#include "../include/fon_types.hpp"
#include <cstring>
#include <stdexcept>

namespace fon {

    // Z85 alphabet: 85 printable ASCII characters
    static constexpr char z85_encode[] =
        "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#";

    // Z85 decode table (maps ASCII 32-127 to 0-84, 255 = invalid)
    static constexpr uint8_t z85_decode[] = {
        255, 68,  255, 84,  83,  82,  72,  255, 75,  76,  70,  65,  255, 63,  62,  69,  // 32-47
        0,   1,   2,   3,   4,   5,   6,   7,   8,   9,   64,  255, 73,  66,  74,  71,  // 48-63
        81,  36,  37,  38,  39,  40,  41,  42,  43,  44,  45,  46,  47,  48,  49,  50,  // 64-79
        51,  52,  53,  54,  55,  56,  57,  58,  59,  60,  61,  77,  255, 78,  67,  255, // 80-95
        255, 10,  11,  12,  13,  14,  15,  16,  17,  18,  19,  20,  21,  22,  23,  24,  // 96-111
        25,  26,  27,  28,  29,  30,  31,  32,  33,  34,  35,  79,  255, 80,  255, 255  // 112-127
    };


    RawData& RawData::pack() {
        if (!encoded_.empty() || data_.empty()) {
            return *this;
        }

        size_t input_len = data_.size();
        int padding = (4 - (input_len % 4)) % 4;
        size_t padded_len = input_len + padding;
        size_t output_len = (padded_len / 4) * 5;

        // +1 for padding marker if needed
        if (padding > 0) {
            output_len += 1;
        }

        encoded_.resize(output_len);
        size_t write_pos = 0;

        // Process full 4-byte blocks
        size_t full_blocks = input_len / 4;
        for (size_t i = 0; i < full_blocks; ++i) {
            size_t offset = i * 4;
            uint32_t value = (static_cast<uint32_t>(data_[offset]) << 24) |
                             (static_cast<uint32_t>(data_[offset + 1]) << 16) |
                             (static_cast<uint32_t>(data_[offset + 2]) << 8) |
                             static_cast<uint32_t>(data_[offset + 3]);

            encoded_[write_pos + 4] = z85_encode[value % 85]; value /= 85;
            encoded_[write_pos + 3] = z85_encode[value % 85]; value /= 85;
            encoded_[write_pos + 2] = z85_encode[value % 85]; value /= 85;
            encoded_[write_pos + 1] = z85_encode[value % 85]; value /= 85;
            encoded_[write_pos] = z85_encode[value];
            write_pos += 5;
        }

        // Handle remaining bytes with padding
        size_t remaining = input_len % 4;
        if (remaining > 0) {
            uint32_t value = 0;
            size_t offset = full_blocks * 4;

            for (size_t i = 0; i < remaining; ++i) {
                value = (value << 8) | data_[offset + i];
            }
            // Pad with zeros
            for (size_t i = remaining; i < 4; ++i) {
                value <<= 8;
            }

            encoded_[write_pos + 4] = z85_encode[value % 85]; value /= 85;
            encoded_[write_pos + 3] = z85_encode[value % 85]; value /= 85;
            encoded_[write_pos + 2] = z85_encode[value % 85]; value /= 85;
            encoded_[write_pos + 1] = z85_encode[value % 85]; value /= 85;
            encoded_[write_pos] = z85_encode[value];
            write_pos += 5;

            // Append padding marker
            encoded_[write_pos] = '0' + padding;
        }

        data_.clear();
        return *this;
    }


    RawData& RawData::unpack() {
        if (!data_.empty() || encoded_.empty()) {
            return *this;
        }

        size_t len = encoded_.size();
        if (len == 0) return *this;

        // Check for padding marker (1, 2, or 3)
        int padding = 0;
        bool has_padding = false;
        char last = encoded_[len - 1];
        if (last >= '1' && last <= '3') {
            padding = last - '0';
            has_padding = true;
            len--; // Exclude padding marker
        }

        size_t output_len = (len / 5) * 4 - padding;
        data_.resize(output_len);

        size_t write_pos = 0;
        for (size_t i = 0; i < len; i += 5) {
            uint32_t value = 0;
            for (size_t j = 0; j < 5; ++j) {
                char c = encoded_[i + j];
                if (c < 32 || c > 127) {
                    throw std::runtime_error("Invalid Z85 character");
                }
                uint8_t decoded = z85_decode[c - 32];
                if (decoded == 255) {
                    throw std::runtime_error("Invalid Z85 character");
                }
                value = value * 85 + decoded;
            }

            if (write_pos < output_len) data_[write_pos++] = (value >> 24) & 0xFF;
            if (write_pos < output_len) data_[write_pos++] = (value >> 16) & 0xFF;
            if (write_pos < output_len) data_[write_pos++] = (value >> 8) & 0xFF;
            if (write_pos < output_len) data_[write_pos++] = value & 0xFF;
        }

        encoded_.clear();
        return *this;
    }

}
