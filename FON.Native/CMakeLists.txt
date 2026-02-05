cmake_minimum_required(VERSION 3.16)
project(fon_native VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Build type
if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release)
endif()

# Optimization flags
if(MSVC)
    add_compile_options(/O2 /Ob2 /Oi /Ot /GL /fp:fast)
    add_link_options(/LTCG)
else()
    add_compile_options(-O3 -ffast-math -fPIC)
    # Use native optimizations only for local builds, not CI
    if(NOT DEFINED ENV{GITHUB_ACTIONS})
        add_compile_options(-march=native)
    endif()
endif()

# Library sources
set(SOURCES
    src/fon_export.cpp
    src/raw_data.cpp
)

# Shared library
add_library(fon_native SHARED ${SOURCES})

target_include_directories(fon_native PUBLIC include)
target_compile_definitions(fon_native PRIVATE FON_NATIVE_EXPORTS)

# Platform-specific settings
if(WIN32)
    set_target_properties(fon_native PROPERTIES
        OUTPUT_NAME "fon_native"
        PREFIX ""
        SUFFIX ".dll"
    )
elseif(APPLE)
    set_target_properties(fon_native PROPERTIES
        OUTPUT_NAME "fon_native"
        PREFIX "lib"
        SUFFIX ".dylib"
    )
else()
    set_target_properties(fon_native PROPERTIES
        OUTPUT_NAME "fon_native"
        PREFIX "lib"
        SUFFIX ".so"
    )
endif()

# Enable parallel STL on MSVC
if(MSVC)
    target_compile_options(fon_native PRIVATE /openmp:experimental)
endif()

# Install
install(TARGETS fon_native
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)
install(FILES include/fon_export.h DESTINATION include)
