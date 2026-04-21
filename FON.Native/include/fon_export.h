#pragma once

#ifdef _WIN32
    #ifdef FON_NATIVE_EXPORTS
        #define FON_API __declspec(dllexport)
    #else
        #define FON_API __declspec(dllimport)
    #endif
#else
    #define FON_API __attribute__((visibility("default")))
#endif

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

    // Error handling
    typedef struct {
        int32_t code;
        char message[256];
    } FonError;

    // Result codes
    #define FON_OK 0
    #define FON_ERROR_FILE_NOT_FOUND 1
    #define FON_ERROR_PARSE_FAILED 2
    #define FON_ERROR_WRITE_FAILED 3
    #define FON_ERROR_INVALID_ARGUMENT 4

    // Opaque handles
    typedef void* FonDumpHandle;
    typedef void* FonCollectionHandle;

    // ==================== SERIALIZATION ====================

    /**
     * Serialize FON dump to file
     * @param dump Handle to FonDump
     * @param path File path (UTF-8 encoded)
     * @param max_threads Maximum threads (0 = auto)
     * @param error Output error info
     * @return FON_OK on success
     */
    FON_API int32_t fon_serialize_to_file(
        FonDumpHandle dump,
        const char* path,
        int32_t max_threads,
        FonError* error
    );

    // ==================== DESERIALIZATION ====================

    /**
     * Deserialize file to FonDump
     * @param path File path (UTF-8 encoded)
     * @param max_threads Maximum threads (0 = auto)
     * @param error Output error info
     * @return FonDump handle or NULL on error
     */
    FON_API FonDumpHandle fon_deserialize_from_file(
        const char* path,
        int32_t max_threads,
        FonError* error
    );

    // ==================== MEMORY MANAGEMENT ====================

    /**
     * Free FonDump handle
     */
    FON_API void fon_dump_free(FonDumpHandle dump);

    /**
     * Get number of collections in dump
     */
    FON_API int64_t fon_dump_size(FonDumpHandle dump);

    /**
     * Get collection by index
     */
    FON_API FonCollectionHandle fon_dump_get(FonDumpHandle dump, uint64_t index);

    // ==================== COLLECTION OPERATIONS ====================

    /**
     * Create new FonDump
     */
    FON_API FonDumpHandle fon_dump_create(void);

    /**
     * Create new FonCollection
     */
    FON_API FonCollectionHandle fon_collection_create(void);

    /**
     * Free FonCollection handle (not needed for collections from dump)
     */
    FON_API void fon_collection_free(FonCollectionHandle collection);

    /**
     * Add collection to dump
     */
    FON_API int32_t fon_dump_add(FonDumpHandle dump, uint64_t id, FonCollectionHandle collection, FonError* error);

    /**
     * Get number of items in collection
     */
    FON_API int64_t fon_collection_size(FonCollectionHandle collection);

    /**
     * Add value to collection
     */
    FON_API int32_t fon_collection_add_int(FonCollectionHandle collection, const char* key, int32_t value, FonError* error);
    FON_API int32_t fon_collection_add_long(FonCollectionHandle collection, const char* key, int64_t value, FonError* error);
    FON_API int32_t fon_collection_add_float(FonCollectionHandle collection, const char* key, float value, FonError* error);
    FON_API int32_t fon_collection_add_double(FonCollectionHandle collection, const char* key, double value, FonError* error);
    FON_API int32_t fon_collection_add_bool(FonCollectionHandle collection, const char* key, int32_t value, FonError* error);
    FON_API int32_t fon_collection_add_string(FonCollectionHandle collection, const char* key, const char* value, FonError* error);

    FON_API int32_t fon_collection_add_int_array(FonCollectionHandle collection, const char* key, const int32_t* values, int64_t count, FonError* error);
    FON_API int32_t fon_collection_add_float_array(FonCollectionHandle collection, const char* key, const float* values, int64_t count, FonError* error);

    /**
     * Get value from collection
     */
    FON_API int32_t fon_collection_get_int(FonCollectionHandle collection, const char* key, int32_t* value, FonError* error);
    FON_API int32_t fon_collection_get_long(FonCollectionHandle collection, const char* key, int64_t* value, FonError* error);
    FON_API int32_t fon_collection_get_float(FonCollectionHandle collection, const char* key, float* value, FonError* error);
    FON_API int32_t fon_collection_get_double(FonCollectionHandle collection, const char* key, double* value, FonError* error);
    FON_API int32_t fon_collection_get_bool(FonCollectionHandle collection, const char* key, int32_t* value, FonError* error);
    FON_API int32_t fon_collection_get_string(FonCollectionHandle collection, const char* key, char* buffer, int64_t buffer_size, FonError* error);

    FON_API int32_t fon_collection_get_int_array(FonCollectionHandle collection, const char* key, int32_t* buffer, int64_t buffer_size, int64_t* actual_size, FonError* error);
    FON_API int32_t fon_collection_get_float_array(FonCollectionHandle collection, const char* key, float* buffer, int64_t buffer_size, int64_t* actual_size, FonError* error);

    /**
     * Add a nested collection to a parent collection.
     * Ownership: parent takes ownership of child. After this call, the child handle
     * is invalidated; the caller must not use it again or call fon_collection_free on it.
     */
    FON_API int32_t fon_collection_add_collection(
        FonCollectionHandle parent,
        const char* key,
        FonCollectionHandle child,
        FonError* error
    );


    /**
     * Add an array of nested collections to a parent collection.
     * Ownership: parent takes ownership of every handle in the array. All children
     * handles are invalidated after the call.
     */
    FON_API int32_t fon_collection_add_collection_array(
        FonCollectionHandle parent,
        const char* key,
        const FonCollectionHandle* children,
        int64_t count,
        FonError* error
    );

    /**
     * Get a borrowed handle to a nested collection. Returns NULL if the key is missing
     * or the value is not a nested object. The returned handle is owned by the parent;
     * the caller must not call fon_collection_free on it.
     */
    FON_API FonCollectionHandle fon_collection_get_collection(
        FonCollectionHandle parent,
        const char* key,
        FonError* error
    );


    /**
     * Get an array of borrowed nested-collection handles. Pass buffer=NULL with
     * buffer_size=0 to query actual_size only. Returns FON_OK on success.
     */
    FON_API int32_t fon_collection_get_collection_array(
        FonCollectionHandle parent,
        const char* key,
        FonCollectionHandle* buffer,
        int64_t buffer_size,
        int64_t* actual_size,
        FonError* error
    );

    // ==================== CONFIGURATION ====================

    /**
     * Set raw data unpacking mode
     */
    FON_API void fon_set_raw_unpack(int32_t enable);

    /**
     * Set the maximum nesting depth for the parser. Values less than 1 are
     * silently clamped to 1. Default: 64.
     */
    FON_API void fon_set_max_depth(int32_t depth);

    /**
     * Get version info
     */
    FON_API const char* fon_version(void);

#ifdef __cplusplus
}
#endif
