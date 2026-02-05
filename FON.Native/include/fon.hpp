#pragma once

#include "fon_types.hpp"
#include <filesystem>
#include <fstream>
#include <sstream>
#include <thread>
#include <future>
#include <mutex>
#include <charconv>
#include <algorithm>

#ifdef _MSC_VER
#include <execution>
#endif

namespace fon {

    /**
     * FON serializer/deserializer with parallel processing support
     */
    class Fon {
    public:
        // Configuration
        static inline bool deserialize_raw_unpack = false;
        static inline int parallel_threshold = 2000;

        // ==================== SERIALIZATION ====================
        static void serialize_to_file(const FonDump& dump, const std::filesystem::path& path, int max_threads = 0);
        static void serialize_to_file_parallel(const FonDump& dump, const std::filesystem::path& path, int max_threads = 0);
        static std::string serialize_to_string(const FonCollection& collection);

        // ==================== DESERIALIZATION ====================
        static FonDump deserialize_from_file(const std::filesystem::path& path, int max_threads = 0);
        static FonDump deserialize_from_file_parallel(const std::filesystem::path& path, int max_threads = 0);
        static FonCollection deserialize_line(std::string_view line);

    private:
        static void serialize_value(std::string& out, const FonValue& value);
        static void serialize_string(std::string& out, std::string_view str);

        template<typename T>
        static void serialize_number(std::string& out, T value);

        template<typename T>
        static void serialize_array(std::string& out, const std::vector<T>& arr, char type_char);

        static std::pair<FonValue, size_t> parse_value(std::string_view data, char type_char);
        static std::pair<std::string, size_t> parse_string(std::string_view data);

        template<typename T>
        static std::pair<T, size_t> parse_number(std::string_view data);

        template<typename T>
        static std::pair<std::vector<T>, size_t> parse_array(std::string_view data, char type_char);

        static char get_type_char(const FonValue& value);
        static size_t find_value_end(std::string_view data);
        static size_t find_closing_bracket(std::string_view data);
    };




    // ==================== IMPLEMENTATION ====================

    inline void Fon::serialize_to_file(const FonDump& dump, const std::filesystem::path& path, int max_threads) {
        serialize_to_file_parallel(dump, path, max_threads);
    }



    inline void Fon::serialize_to_file_parallel(const FonDump& dump, const std::filesystem::path& path, int max_threads) {
        if (max_threads <= 0) {
            max_threads = std::thread::hardware_concurrency();
        }

        std::vector<std::pair<uint64_t, const FonCollection*>> entries;
        entries.reserve(dump.size());
        for (const auto& [id, collection] : dump) {
            entries.emplace_back(id, &collection);
        }
        std::sort(entries.begin(), entries.end(), [](const auto& a, const auto& b) {
            return a.first < b.first;
        });

        std::vector<std::string> lines(entries.size());

    #ifdef _MSC_VER
        std::for_each(std::execution::par, entries.begin(), entries.end(),
            [&lines, &entries](const auto& entry) {
                size_t idx = &entry - entries.data();
                lines[idx] = serialize_to_string(*entry.second);
            });
    #else
        std::vector<std::future<void>> futures;
        size_t chunk_size = (entries.size() + max_threads - 1) / max_threads;

        for (int t = 0; t < max_threads; ++t) {
            size_t start = t * chunk_size;
            size_t end = std::min(start + chunk_size, entries.size());
            if (start >= entries.size()) break;

            futures.push_back(std::async(std::launch::async, [&, start, end]() {
                for (size_t i = start; i < end; ++i) {
                    lines[i] = serialize_to_string(*entries[i].second);
                }
            }));
        }
        for (auto& f : futures) f.get();
    #endif

        std::ofstream file(path, std::ios::binary);
        if (!file) {
            throw std::runtime_error("Failed to open file for writing: " + path.string());
        }

        for (const auto& line : lines) {
            file << line << "\n";
        }
    }



    inline std::string Fon::serialize_to_string(const FonCollection& collection) {
        std::string result;
        result.reserve(4096);

        bool first = true;
        for (const auto& [key, value] : collection) {
            if (!first) result += ',';
            first = false;

            result += key;
            result += '=';
            result += get_type_char(value);
            result += ':';
            serialize_value(result, value);
        }

        return result;
    }



    inline void Fon::serialize_value(std::string& out, const FonValue& value) {
        std::visit([&out](const auto& v) {
            using T = std::decay_t<decltype(v)>;

            if constexpr (std::is_same_v<T, uint8_t> || std::is_same_v<T, int16_t> ||
                        std::is_same_v<T, int32_t> || std::is_same_v<T, uint32_t> ||
                        std::is_same_v<T, int64_t> || std::is_same_v<T, uint64_t> ||
                        std::is_same_v<T, float> || std::is_same_v<T, double>
            ) {
                serialize_number(out, v);
            }
            else if constexpr (std::is_same_v<T, bool>) {
                out += v ? '1' : '0';
            }
            else if constexpr (std::is_same_v<T, std::string>) {
                serialize_string(out, v);
            }
            else if constexpr (std::is_same_v<T, std::shared_ptr<RawData>>) {
                out += '"';
                if (v) {
                    auto& raw = const_cast<RawData&>(*v);
                    out += raw.pack().encoded();
                }
                out += '"';
            }
            else if constexpr (std::is_same_v<T, std::vector<float>>) {
                serialize_array(out, v, TYPE_FLOAT);
            }
            else if constexpr (std::is_same_v<T, std::vector<double>>) {
                serialize_array(out, v, TYPE_DOUBLE);
            }
            else if constexpr (std::is_same_v<T, std::vector<int32_t>>) {
                serialize_array(out, v, TYPE_INT);
            }
            else if constexpr (std::is_same_v<T, std::vector<uint8_t>>) {
                serialize_array(out, v, TYPE_BYTE);
            }
            else if constexpr (std::is_same_v<T, std::vector<int16_t>>) {
                serialize_array(out, v, TYPE_SHORT);
            }
            else if constexpr (std::is_same_v<T, std::vector<uint32_t>>) {
                serialize_array(out, v, TYPE_UINT);
            }
            else if constexpr (std::is_same_v<T, std::vector<int64_t>>) {
                serialize_array(out, v, TYPE_LONG);
            }
            else if constexpr (std::is_same_v<T, std::vector<uint64_t>>) {
                serialize_array(out, v, TYPE_ULONG);
            }
            else if constexpr (std::is_same_v<T, std::vector<bool>>) {
                out += '[';
                for (size_t i = 0; i < v.size(); ++i) {
                    if (i > 0) out += ',';
                    out += v[i] ? '1' : '0';
                }
                out += ']';
            }
            else if constexpr (std::is_same_v<T, std::vector<std::string>>) {
                out += '[';
                for (size_t i = 0; i < v.size(); ++i) {
                    if (i > 0) out += ',';
                    serialize_string(out, v[i]);
                }
                out += ']';
            }
        }, value);
    }



    template<typename T>
    inline void Fon::serialize_number(std::string& out, T value) {
        char buffer[32];
        auto result = std::to_chars(buffer, buffer + sizeof(buffer), value);
        out.append(buffer, result.ptr - buffer);
    }



    template<typename T>
    inline void Fon::serialize_array(std::string& out, const std::vector<T>& arr, char type_char) {
        out += '[';
        for (size_t i = 0; i < arr.size(); ++i) {
            if (i > 0) out += ',';
            serialize_number(out, arr[i]);
        }
        out += ']';
    }



    inline void Fon::serialize_string(std::string& out, std::string_view str) {
        out += '"';
        for (char c : str) {
            switch (c) {
                case '"':  out += "\\\""; break;
                case '\\': out += "\\\\"; break;
                case '\n': out += "\\n"; break;
                case '\r': out += "\\r"; break;
                case '\t': out += "\\t"; break;
                case '\b': out += "\\b"; break;
                case '\f': out += "\\f"; break;
                default:
                    if (static_cast<unsigned char>(c) < 32) {
                        char buf[7];
                        snprintf(buf, sizeof(buf), "\\u%04X", static_cast<unsigned char>(c));
                        out += buf;
                    } else {
                        out += c;
                    }
            }
        }
        out += '"';
    }



    inline char Fon::get_type_char(const FonValue& value) {
        return std::visit([](const auto& v) -> char {
            using T = std::decay_t<decltype(v)>;
            if constexpr (std::is_same_v<T, uint8_t>) return TYPE_BYTE;
            else if constexpr (std::is_same_v<T, int16_t>) return TYPE_SHORT;
            else if constexpr (std::is_same_v<T, int32_t>) return TYPE_INT;
            else if constexpr (std::is_same_v<T, uint32_t>) return TYPE_UINT;
            else if constexpr (std::is_same_v<T, int64_t>) return TYPE_LONG;
            else if constexpr (std::is_same_v<T, uint64_t>) return TYPE_ULONG;
            else if constexpr (std::is_same_v<T, float>) return TYPE_FLOAT;
            else if constexpr (std::is_same_v<T, double>) return TYPE_DOUBLE;
            else if constexpr (std::is_same_v<T, bool>) return TYPE_BOOL;
            else if constexpr (std::is_same_v<T, std::string>) return TYPE_STRING;
            else if constexpr (std::is_same_v<T, std::shared_ptr<RawData>>) return TYPE_RAW;
            else if constexpr (std::is_same_v<T, std::vector<float>>) return TYPE_FLOAT;
            else if constexpr (std::is_same_v<T, std::vector<double>>) return TYPE_DOUBLE;
            else if constexpr (std::is_same_v<T, std::vector<int32_t>>) return TYPE_INT;
            else if constexpr (std::is_same_v<T, std::vector<uint8_t>>) return TYPE_BYTE;
            else if constexpr (std::is_same_v<T, std::vector<int16_t>>) return TYPE_SHORT;
            else if constexpr (std::is_same_v<T, std::vector<uint32_t>>) return TYPE_UINT;
            else if constexpr (std::is_same_v<T, std::vector<int64_t>>) return TYPE_LONG;
            else if constexpr (std::is_same_v<T, std::vector<uint64_t>>) return TYPE_ULONG;
            else if constexpr (std::is_same_v<T, std::vector<bool>>) return TYPE_BOOL;
            else if constexpr (std::is_same_v<T, std::vector<std::string>>) return TYPE_STRING;
            else return '?';
        }, value);
    }



    // ==================== DESERIALIZATION ====================

    inline FonDump Fon::deserialize_from_file(const std::filesystem::path& path, int max_threads) {
        return deserialize_from_file_parallel(path, max_threads);
    }



    inline FonDump Fon::deserialize_from_file_parallel(const std::filesystem::path& path, int max_threads) {
        if (max_threads <= 0) {
            max_threads = std::thread::hardware_concurrency();
        }

        std::ifstream file(path, std::ios::binary | std::ios::ate);
        if (!file) {
            throw std::runtime_error("Failed to open file: " + path.string());
        }

        auto file_size = file.tellg();
        file.seekg(0);

        std::string content(file_size, '\0');
        file.read(content.data(), file_size);

        std::vector<std::string_view> lines;
        lines.reserve(file_size / 1000);

        size_t start = 0;
        for (size_t i = 0; i < content.size(); ++i) {
            if (content[i] == '\n' || content[i] == '\r') {
                if (i > start) {
                    lines.emplace_back(content.data() + start, i - start);
                }
                if (content[i] == '\r' && i + 1 < content.size() && content[i + 1] == '\n') {
                    ++i;
                }
                start = i + 1;
            }
        }
        if (start < content.size()) {
            lines.emplace_back(content.data() + start, content.size() - start);
        }

        std::vector<FonCollection> collections(lines.size());

    #ifdef _MSC_VER
        std::vector<size_t> indices(lines.size());
        std::iota(indices.begin(), indices.end(), 0);

        std::for_each(std::execution::par, indices.begin(), indices.end(),
            [&](size_t i) {
                if (!lines[i].empty()) {
                    collections[i] = deserialize_line(lines[i]);
                }
            });
    #else
        std::vector<std::future<void>> futures;
        size_t chunk_size = (lines.size() + max_threads - 1) / max_threads;

        for (int t = 0; t < max_threads; ++t) {
            size_t start_idx = t * chunk_size;
            size_t end_idx = std::min(start_idx + chunk_size, lines.size());
            if (start_idx >= lines.size()) break;

            futures.push_back(std::async(std::launch::async, [&, start_idx, end_idx]() {
                for (size_t i = start_idx; i < end_idx; ++i) {
                    if (!lines[i].empty()) {
                        collections[i] = deserialize_line(lines[i]);
                    }
                }
            }));
        }
        for (auto& f : futures) f.get();
    #endif

        FonDump dump(lines.size());
        for (size_t i = 0; i < collections.size(); ++i) {
            if (collections[i].size() > 0) {
                dump.add(i, std::move(collections[i]));
            }
        }

        return dump;
    }



    inline FonCollection Fon::deserialize_line(std::string_view line) {
        FonCollection collection;
        size_t pos = 0;

        while (pos < line.size()) {
            auto eq_pos = line.find('=', pos);
            if (eq_pos == std::string_view::npos) break;

            std::string key(line.substr(pos, eq_pos - pos));
            pos = eq_pos + 1;

            if (pos >= line.size() || pos + 1 >= line.size() || line[pos + 1] != ':') {
                throw std::runtime_error("Invalid format: expected type:value");
            }

            char type_char = line[pos];
            pos += 2;

            auto remaining = line.substr(pos);
            auto [value, consumed] = parse_value(remaining, type_char);

            collection.add(key, std::move(value));
            pos += consumed;

            if (pos < line.size() && line[pos] == ',') {
                ++pos;
            }
        }

        return collection;
    }



    inline std::pair<FonValue, size_t> Fon::parse_value(std::string_view data, char type_char) {
        if (data.empty()) {
            throw std::runtime_error("Empty value");
        }

        if (data[0] == '[') {
            switch (type_char) {
                case TYPE_BYTE:   { auto [v, c] = parse_array<uint8_t>(data, type_char); return {std::move(v), c}; }
                case TYPE_SHORT:  { auto [v, c] = parse_array<int16_t>(data, type_char); return {std::move(v), c}; }
                case TYPE_INT:    { auto [v, c] = parse_array<int32_t>(data, type_char); return {std::move(v), c}; }
                case TYPE_UINT:   { auto [v, c] = parse_array<uint32_t>(data, type_char); return {std::move(v), c}; }
                case TYPE_LONG:   { auto [v, c] = parse_array<int64_t>(data, type_char); return {std::move(v), c}; }
                case TYPE_ULONG:  { auto [v, c] = parse_array<uint64_t>(data, type_char); return {std::move(v), c}; }
                case TYPE_FLOAT:  { auto [v, c] = parse_array<float>(data, type_char); return {std::move(v), c}; }
                case TYPE_DOUBLE: { auto [v, c] = parse_array<double>(data, type_char); return {std::move(v), c}; }
                default: throw std::runtime_error("Unsupported array type");
            }
        }

        if (type_char == TYPE_STRING) {
            auto [str, consumed] = parse_string(data);
            return {std::move(str), consumed};
        }

        if (type_char == TYPE_RAW) {
            auto [str, consumed] = parse_string(data);
            auto raw = std::make_shared<RawData>(str);
            if (deserialize_raw_unpack) {
                raw->unpack();
            }
            return {raw, consumed};
        }

        size_t end = find_value_end(data);
        auto value_str = data.substr(0, end);
        size_t consumed = end;
        if (consumed < data.size() && data[consumed] == ',') ++consumed;

        switch (type_char) {
            case TYPE_BYTE:   { auto [v, c] = parse_number<uint8_t>(value_str); return {v, consumed}; }
            case TYPE_SHORT:  { auto [v, c] = parse_number<int16_t>(value_str); return {v, consumed}; }
            case TYPE_INT:    { auto [v, c] = parse_number<int32_t>(value_str); return {v, consumed}; }
            case TYPE_UINT:   { auto [v, c] = parse_number<uint32_t>(value_str); return {v, consumed}; }
            case TYPE_LONG:   { auto [v, c] = parse_number<int64_t>(value_str); return {v, consumed}; }
            case TYPE_ULONG:  { auto [v, c] = parse_number<uint64_t>(value_str); return {v, consumed}; }
            case TYPE_FLOAT:  { auto [v, c] = parse_number<float>(value_str); return {v, consumed}; }
            case TYPE_DOUBLE: { auto [v, c] = parse_number<double>(value_str); return {v, consumed}; }
            case TYPE_BOOL:   return {value_str[0] != '0', consumed};
            default: throw std::runtime_error("Unknown type");
        }
    }



    template<typename T>
    inline std::pair<T, size_t> Fon::parse_number(std::string_view data) {
        T value{};

        if constexpr (std::is_floating_point_v<T>) {
    #if defined(_MSC_VER) || (defined(__cpp_lib_to_chars) && __cpp_lib_to_chars >= 201611L)
            auto [ptr, ec] = std::from_chars(data.data(), data.data() + data.size(), value);
            if (ec == std::errc{}) {
                return {value, static_cast<size_t>(ptr - data.data())};
            }
    #endif
            size_t end_pos = find_value_end(data);
            std::string temp(data.substr(0, end_pos));
            if constexpr (std::is_same_v<T, float>) {
                value = std::strtof(temp.c_str(), nullptr);
            } else {
                value = std::strtod(temp.c_str(), nullptr);
            }
            return {value, end_pos};
        } else {
            auto [ptr, ec] = std::from_chars(data.data(), data.data() + data.size(), value);
            if (ec != std::errc{}) {
                throw std::runtime_error("Failed to parse number");
            }
            return {value, static_cast<size_t>(ptr - data.data())};
        }
    }



    template<typename T>
    inline std::pair<std::vector<T>, size_t> Fon::parse_array(std::string_view data, char type_char) {
        if (data[0] != '[') {
            throw std::runtime_error("Array must start with '['");
        }

        size_t close = find_closing_bracket(data);
        auto content = data.substr(1, close - 1);

        std::vector<T> result;
        result.reserve(content.size() / 4);

        size_t pos = 0;
        while (pos < content.size()) {
            auto remaining = content.substr(pos);
            auto [value, consumed] = parse_number<T>(remaining);
            result.push_back(value);
            pos += consumed;
            if (pos < content.size() && content[pos] == ',') ++pos;
        }

        size_t total_consumed = close + 1;
        if (total_consumed < data.size() && data[total_consumed] == ',') ++total_consumed;

        return {std::move(result), total_consumed};
    }



    inline std::pair<std::string, size_t> Fon::parse_string(std::string_view data) {
        if (data[0] != '"') {
            throw std::runtime_error("String must start with '\"'");
        }

        size_t end_quote = 1;
        while (end_quote < data.size()) {
            if (data[end_quote] == '"' && data[end_quote - 1] != '\\') {
                break;
            }
            ++end_quote;
        }

        auto content = data.substr(1, end_quote - 1);

        if (content.find('\\') == std::string_view::npos) {
            size_t consumed = end_quote + 1;
            if (consumed < data.size() && data[consumed] == ',') ++consumed;
            return {std::string(content), consumed};
        }

        std::string result;
        result.reserve(content.size());

        for (size_t i = 0; i < content.size(); ++i) {
            if (content[i] == '\\' && i + 1 < content.size()) {
                ++i;
                switch (content[i]) {
                    case '"':  result += '"'; break;
                    case '\\': result += '\\'; break;
                    case 'n':  result += '\n'; break;
                    case 'r':  result += '\r'; break;
                    case 't':  result += '\t'; break;
                    case 'b':  result += '\b'; break;
                    case 'f':  result += '\f'; break;
                    default:   result += content[i]; break;
                }
            } else {
                result += content[i];
            }
        }

        size_t consumed = end_quote + 1;
        if (consumed < data.size() && data[consumed] == ',') ++consumed;
        return {std::move(result), consumed};
    }



    inline size_t Fon::find_value_end(std::string_view data) {
        for (size_t i = 0; i < data.size(); ++i) {
            char c = data[i];
            if (c == ',' || c == ']' || c == '\r' || c == '\n') {
                return i;
            }
        }
        return data.size();
    }



    inline size_t Fon::find_closing_bracket(std::string_view data) {
        int depth = 0;
        bool in_string = false;

        for (size_t i = 0; i < data.size(); ++i) {
            char c = data[i];

            if (c == '"' && (i == 0 || data[i - 1] != '\\')) {
                in_string = !in_string;
            } else if (!in_string) {
                if (c == '[') ++depth;
                else if (c == ']') {
                    --depth;
                    if (depth == 0) return i;
                }
            }
        }

        throw std::runtime_error("Closing bracket not found");
    }

}
