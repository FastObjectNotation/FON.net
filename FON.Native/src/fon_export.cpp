#include "../include/fon_export.h"
#include "../include/fon.hpp"
#include <cstring>

static void set_error(FonError* error, int32_t code, const char* message) {
    if (error) {
        error->code = code;
        strncpy(error->message, message, sizeof(error->message) - 1);
        error->message[sizeof(error->message) - 1] = '\0';
    }
}



// ==================== VERSION ====================

FON_API const char* fon_version(void) {
    return "1.0.0";
}



// ==================== CONFIGURATION ====================

FON_API void fon_set_raw_unpack(int32_t enable) {
    fon::Fon::deserialize_raw_unpack = enable != 0;
}


FON_API void fon_set_max_depth(int32_t depth) {
    if (depth < 1) {
        depth = 1;
    }
    fon::Fon::max_depth = depth;
}



// ==================== MEMORY MANAGEMENT ====================

FON_API FonDumpHandle fon_dump_create(void) {
    return new fon::FonDump();
}


FON_API void fon_dump_free(FonDumpHandle dump) {
    delete static_cast<fon::FonDump*>(dump);
}


FON_API int64_t fon_dump_size(FonDumpHandle dump) {
    if (!dump) return 0;
    return static_cast<int64_t>(static_cast<fon::FonDump*>(dump)->size());
}


FON_API FonCollectionHandle fon_dump_get(FonDumpHandle dump, uint64_t index) {
    if (!dump) return nullptr;
    auto* d = static_cast<fon::FonDump*>(dump);
    auto* collection = d->try_get(index);
    return collection;
}


FON_API FonCollectionHandle fon_collection_create(void) {
    return new fon::FonCollection();
}


FON_API void fon_collection_free(FonCollectionHandle collection) {
    delete static_cast<fon::FonCollection*>(collection);
}


FON_API int64_t fon_collection_size(FonCollectionHandle collection) {
    if (!collection) return 0;
    return static_cast<int64_t>(static_cast<fon::FonCollection*>(collection)->size());
}



// ==================== SERIALIZATION ====================

FON_API int32_t fon_serialize_to_file(
    FonDumpHandle dump,
    const char* path,
    int32_t max_threads,
    FonError* error
) {
    if (!dump || !path) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument: dump or path is null");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    try {
        auto* d = static_cast<fon::FonDump*>(dump);
        fon::Fon::serialize_to_file(*d, path, max_threads);
        return FON_OK;
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_WRITE_FAILED, e.what());
        return FON_ERROR_WRITE_FAILED;
    }
}



// ==================== DESERIALIZATION ====================

FON_API FonDumpHandle fon_deserialize_from_file(
    const char* path,
    int32_t max_threads,
    FonError* error
) {
    if (!path) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument: path is null");
        return nullptr;
    }

    try {
        auto dump = fon::Fon::deserialize_from_file(path, max_threads);
        return new fon::FonDump(std::move(dump));
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_PARSE_FAILED, e.what());
        return nullptr;
    }
}



// ==================== COLLECTION ADD OPERATIONS ====================

FON_API int32_t fon_dump_add(FonDumpHandle dump, uint64_t id, FonCollectionHandle collection, FonError* error) {
    if (!dump || !collection) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    try {
        auto* d = static_cast<fon::FonDump*>(dump);
        auto* c = static_cast<fon::FonCollection*>(collection);
        d->add(id, std::move(*c));
        return FON_OK;
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, e.what());
        return FON_ERROR_INVALID_ARGUMENT;
    }
}


FON_API int32_t fon_collection_add_int(FonCollectionHandle collection, const char* key, int32_t value, FonError* error) {
    if (!collection || !key) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    auto* c = static_cast<fon::FonCollection*>(collection);
    c->add(key, value);
    return FON_OK;
}


FON_API int32_t fon_collection_add_long(FonCollectionHandle collection, const char* key, int64_t value, FonError* error) {
    if (!collection || !key) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    auto* c = static_cast<fon::FonCollection*>(collection);
    c->add(key, value);
    return FON_OK;
}


FON_API int32_t fon_collection_add_float(FonCollectionHandle collection, const char* key, float value, FonError* error) {
    if (!collection || !key) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    auto* c = static_cast<fon::FonCollection*>(collection);
    c->add(key, value);
    return FON_OK;
}


FON_API int32_t fon_collection_add_double(FonCollectionHandle collection, const char* key, double value, FonError* error) {
    if (!collection || !key) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    auto* c = static_cast<fon::FonCollection*>(collection);
    c->add(key, value);
    return FON_OK;
}


FON_API int32_t fon_collection_add_bool(FonCollectionHandle collection, const char* key, int32_t value, FonError* error) {
    if (!collection || !key) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    auto* c = static_cast<fon::FonCollection*>(collection);
    c->add(key, value != 0);
    return FON_OK;
}


FON_API int32_t fon_collection_add_string(FonCollectionHandle collection, const char* key, const char* value, FonError* error) {
    if (!collection || !key || !value) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    auto* c = static_cast<fon::FonCollection*>(collection);
    c->add(key, std::string(value));
    return FON_OK;
}


FON_API int32_t fon_collection_add_int_array(FonCollectionHandle collection, const char* key, const int32_t* values, int64_t count, FonError* error) {
    if (!collection || !key || !values || count < 0) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    auto* c = static_cast<fon::FonCollection*>(collection);
    std::vector<int32_t> vec(values, values + count);
    c->add(key, std::move(vec));
    return FON_OK;
}


FON_API int32_t fon_collection_add_collection(FonCollectionHandle parent, const char* key, FonCollectionHandle child, FonError* error) {
    if (!parent || !key || !child) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    auto* p = static_cast<fon::FonCollection*>(parent);
    auto* c = static_cast<fon::FonCollection*>(child);
    p->add(key, std::shared_ptr<fon::FonCollection>(c));
    return FON_OK;
}


FON_API int32_t fon_collection_add_collection_array(FonCollectionHandle parent, const char* key, const FonCollectionHandle* children, int64_t count, FonError* error) {
    if (!parent || !key || (count > 0 && !children) || count < 0) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    auto* p = static_cast<fon::FonCollection*>(parent);
    std::vector<std::shared_ptr<fon::FonCollection>> vec;
    vec.reserve(static_cast<size_t>(count));
    for (int64_t i = 0; i < count; ++i) {
        auto* c = static_cast<fon::FonCollection*>(children[i]);
        vec.emplace_back(c);
    }
    p->add(key, std::move(vec));
    return FON_OK;
}


FON_API int32_t fon_collection_add_float_array(FonCollectionHandle collection, const char* key, const float* values, int64_t count, FonError* error) {
    if (!collection || !key || !values || count < 0) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    auto* c = static_cast<fon::FonCollection*>(collection);
    std::vector<float> vec(values, values + count);
    c->add(key, std::move(vec));
    return FON_OK;
}



// ==================== COLLECTION GET OPERATIONS ====================

FON_API int32_t fon_collection_get_int(FonCollectionHandle collection, const char* key, int32_t* value, FonError* error) {
    if (!collection || !key || !value) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    try {
        auto* c = static_cast<fon::FonCollection*>(collection);
        *value = c->get<int32_t>(key);
        return FON_OK;
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, e.what());
        return FON_ERROR_INVALID_ARGUMENT;
    }
}


FON_API int32_t fon_collection_get_long(FonCollectionHandle collection, const char* key, int64_t* value, FonError* error) {
    if (!collection || !key || !value) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    try {
        auto* c = static_cast<fon::FonCollection*>(collection);
        *value = c->get<int64_t>(key);
        return FON_OK;
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, e.what());
        return FON_ERROR_INVALID_ARGUMENT;
    }
}


FON_API int32_t fon_collection_get_float(FonCollectionHandle collection, const char* key, float* value, FonError* error) {
    if (!collection || !key || !value) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    try {
        auto* c = static_cast<fon::FonCollection*>(collection);
        *value = c->get<float>(key);
        return FON_OK;
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, e.what());
        return FON_ERROR_INVALID_ARGUMENT;
    }
}


FON_API int32_t fon_collection_get_double(FonCollectionHandle collection, const char* key, double* value, FonError* error) {
    if (!collection || !key || !value) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    try {
        auto* c = static_cast<fon::FonCollection*>(collection);
        *value = c->get<double>(key);
        return FON_OK;
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, e.what());
        return FON_ERROR_INVALID_ARGUMENT;
    }
}


FON_API int32_t fon_collection_get_bool(FonCollectionHandle collection, const char* key, int32_t* value, FonError* error) {
    if (!collection || !key || !value) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    try {
        auto* c = static_cast<fon::FonCollection*>(collection);
        *value = c->get<bool>(key) ? 1 : 0;
        return FON_OK;
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, e.what());
        return FON_ERROR_INVALID_ARGUMENT;
    }
}


FON_API int32_t fon_collection_get_string(FonCollectionHandle collection, const char* key, char* buffer, int64_t buffer_size, FonError* error) {
    if (!collection || !key || !buffer || buffer_size <= 0) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    try {
        auto* c = static_cast<fon::FonCollection*>(collection);
        const auto& str = c->get<std::string>(key);
        strncpy(buffer, str.c_str(), buffer_size - 1);
        buffer[buffer_size - 1] = '\0';
        return FON_OK;
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, e.what());
        return FON_ERROR_INVALID_ARGUMENT;
    }
}


FON_API int32_t fon_collection_get_int_array(FonCollectionHandle collection, const char* key, int32_t* buffer, int64_t buffer_size, int64_t* actual_size, FonError* error) {
    if (!collection || !key || !actual_size) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    try {
        auto* c = static_cast<fon::FonCollection*>(collection);
        const auto& arr = c->get<std::vector<int32_t>>(key);
        *actual_size = static_cast<int64_t>(arr.size());

        if (buffer && buffer_size > 0) {
            int64_t copy_count = std::min(buffer_size, *actual_size);
            std::memcpy(buffer, arr.data(), copy_count * sizeof(int32_t));
        }
        return FON_OK;
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, e.what());
        return FON_ERROR_INVALID_ARGUMENT;
    }
}


FON_API int32_t fon_collection_get_float_array(FonCollectionHandle collection, const char* key, float* buffer, int64_t buffer_size, int64_t* actual_size, FonError* error) {
    if (!collection || !key || !actual_size) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    try {
        auto* c = static_cast<fon::FonCollection*>(collection);
        const auto& arr = c->get<std::vector<float>>(key);
        *actual_size = static_cast<int64_t>(arr.size());

        if (buffer && buffer_size > 0) {
            int64_t copy_count = std::min(buffer_size, *actual_size);
            std::memcpy(buffer, arr.data(), copy_count * sizeof(float));
        }
        return FON_OK;
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, e.what());
        return FON_ERROR_INVALID_ARGUMENT;
    }
}


FON_API FonCollectionHandle fon_collection_get_collection(FonCollectionHandle parent, const char* key, FonError* error) {
    if (!parent || !key) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return nullptr;
    }

    try {
        auto* p = static_cast<fon::FonCollection*>(parent);
        auto* shared = p->try_get<std::shared_ptr<fon::FonCollection>>(key);
        if (!shared || !*shared) {
            set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or not a nested collection");
            return nullptr;
        }
        return shared->get();
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, e.what());
        return nullptr;
    }
}


FON_API int32_t fon_collection_get_collection_array(FonCollectionHandle parent, const char* key, FonCollectionHandle* buffer, int64_t buffer_size, int64_t* actual_size, FonError* error) {
    if (!parent || !key || !actual_size) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    try {
        auto* p = static_cast<fon::FonCollection*>(parent);
        auto* vec = p->try_get<std::vector<std::shared_ptr<fon::FonCollection>>>(key);
        if (!vec) {
            set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or not an array of nested collections");
            return FON_ERROR_INVALID_ARGUMENT;
        }

        *actual_size = static_cast<int64_t>(vec->size());

        if (buffer && buffer_size > 0) {
            int64_t copy_count = std::min(buffer_size, *actual_size);
            for (int64_t i = 0; i < copy_count; ++i) {
                buffer[i] = (*vec)[i].get();
            }
        }
        return FON_OK;
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, e.what());
        return FON_ERROR_INVALID_ARGUMENT;
    }
}
