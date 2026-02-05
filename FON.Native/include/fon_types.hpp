#pragma once

#include <string>
#include <string_view>
#include <vector>
#include <unordered_map>
#include <variant>
#include <memory>
#include <cstdint>
#include <stdexcept>

namespace fon {

    // Forward declarations
    class RawData;
    class FonCollection;
    class FonDump;

    // Supported value types
    using FonValue = std::variant<
        uint8_t,                    // 'e' - byte
        int16_t,                    // 't' - short
        int32_t,                    // 'i' - int
        uint32_t,                   // 'u' - uint
        int64_t,                    // 'l' - long
        uint64_t,                   // 'g' - ulong
        float,                      // 'f' - float
        double,                     // 'd' - double
        bool,                       // 'b' - bool
        std::string,                // 's' - string
        std::shared_ptr<RawData>,   // 'r' - raw data
        std::vector<uint8_t>,
        std::vector<int16_t>,
        std::vector<int32_t>,
        std::vector<uint32_t>,
        std::vector<int64_t>,
        std::vector<uint64_t>,
        std::vector<float>,
        std::vector<double>,
        std::vector<bool>,
        std::vector<std::string>
    >;

    // Type codes
    constexpr char TYPE_BYTE   = 'e';
    constexpr char TYPE_SHORT  = 't';
    constexpr char TYPE_INT    = 'i';
    constexpr char TYPE_UINT   = 'u';
    constexpr char TYPE_LONG   = 'l';
    constexpr char TYPE_ULONG  = 'g';
    constexpr char TYPE_FLOAT  = 'f';
    constexpr char TYPE_DOUBLE = 'd';
    constexpr char TYPE_BOOL   = 'b';
    constexpr char TYPE_STRING = 's';
    constexpr char TYPE_RAW    = 'r';



    /**
     * Raw binary data with Z85 (ZeroMQ Base-85) encoding support.
     * Z85 is more efficient than Base64: 25% overhead vs 33%.
     */
    class RawData {
    public:
        RawData() = default;
        explicit RawData(std::vector<uint8_t> data) : data_(std::move(data)) {}
        explicit RawData(std::string_view encoded) : encoded_(encoded) {}

        const std::vector<uint8_t>& data() const { return data_; }
        const std::string& encoded() const { return encoded_; }

        RawData& pack();
        RawData& unpack();

        bool is_packed() const { return !encoded_.empty(); }
        bool is_unpacked() const { return !data_.empty(); }

    private:
        std::vector<uint8_t> data_;
        std::string encoded_;
    };



    /**
     * Collection of key-value pairs (one line in FON format)
     */
    class FonCollection {
    public:
        FonCollection() = default;

        void add(const std::string& key, FonValue value) {
            data_[key] = std::move(value);
        }

        bool contains(const std::string& key) const {
            return data_.find(key) != data_.end();
        }

        const FonValue& get(const std::string& key) const {
            return data_.at(key);
        }

        template<typename T>
        const T& get(const std::string& key) const {
            return std::get<T>(data_.at(key));
        }

        template<typename T>
        T* try_get(const std::string& key) {
            auto it = data_.find(key);
            if (it == data_.end()) return nullptr;
            return std::get_if<T>(&it->second);
        }

        size_t size() const { return data_.size(); }

        auto begin() { return data_.begin(); }
        auto end() { return data_.end(); }
        auto begin() const { return data_.begin(); }
        auto end() const { return data_.end(); }

    private:
        std::unordered_map<std::string, FonValue> data_;
    };



    /**
     * Container for multiple FonCollections (entire FON file)
     */
    class FonDump {
    public:
        FonDump() = default;
        explicit FonDump(size_t capacity) {
            data_.reserve(capacity);
        }

        void add(uint64_t id, FonCollection collection) {
            data_[id] = std::move(collection);
        }

        bool try_add(uint64_t id, FonCollection collection) {
            return data_.try_emplace(id, std::move(collection)).second;
        }

        const FonCollection& get(uint64_t id) const {
            return data_.at(id);
        }

        FonCollection* try_get(uint64_t id) {
            auto it = data_.find(id);
            return it != data_.end() ? &it->second : nullptr;
        }

        size_t size() const { return data_.size(); }

        auto begin() { return data_.begin(); }
        auto end() { return data_.end(); }
        auto begin() const { return data_.begin(); }
        auto end() const { return data_.end(); }

    private:
        std::unordered_map<uint64_t, FonCollection> data_;
    };

}
