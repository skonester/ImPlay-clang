cmake_minimum_required(VERSION 3.24)

list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")

include(GetGitVersion)
include(FetchContent)

get_git_version(GIT_VERSION SEM_VER)

project(ImPlay VERSION "${SEM_VER}")
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include(CMakeDependentOption)

option(USE_OPENGL_ES3 "Compile with OpenGL ES 3.0 loader" OFF)
option(USE_PATCHED_GLFW "Use patched GLFW to support additional features" OFF)
option(CREATE_PACKAGE "Create binary packages with CPack" OFF)
cmake_dependent_option(USE_MPV_WIN_BUILD "Use Prebuilt static mpv dll on Windows" ON "WIN32" OFF)
cmake_dependent_option(USE_XDG_PORTAL "Use xdg-desktop-portal for file dialogs on Linux" OFF "UNIX;NOT APPLE" OFF)

set(CMAKE_POLICY_VERSION_MINIMUM 3.5)
include(FetchContent)

FetchContent_Declare(
  freetype
  URL https://github.com/freetype/freetype/archive/refs/tags/VER-2-13-2.zip
)
FetchContent_MakeAvailable(freetype)

find_package(Threads REQUIRED)

if(USE_MPV_WIN_BUILD)
  include(GetMpvWinDev)
  get_mpv_win_dev(mpv_dev)
endif()

if(USE_PATCHED_GLFW OR WIN32)
  add_subdirectory(third_party/glfw)
  set(GLFW_LIBRARIES glfw)
endif()

set(LIBROMFS_PROJECT_NAME ${PROJECT_NAME})
set(LIBROMFS_RESOURCE_LOCATION "${CMAKE_SOURCE_DIR}/resources/romfs")
set(OPENGL_LIBRARIES "glad")

# Clang support on Windows
if(WIN32 AND CMAKE_CXX_COMPILER_ID MATCHES "Clang")
  add_compile_options(/W4 -Qunused-arguments)
  add_definitions(-D_CRT_SECURE_NO_WARNINGS -DFMT_COMPILE_TIME_CHECKS=0 -DNOMINMAX)
endif()

FetchContent_Declare(
  fmt
  URL https://github.com/fmtlib/fmt/archive/refs/tags/9.1.0.zip
)
FetchContent_Declare(
  json
  URL https://github.com/nlohmann/json/archive/refs/tags/v3.11.3.zip
)
FetchContent_Declare(
  freetype
  URL https://github.com/freetype/freetype/archive/refs/tags/VER-2-13-2.zip
)
FetchContent_MakeAvailable(fmt json freetype)

add_subdirectory(third_party/glad)
add_subdirectory(third_party/natsort)
add_subdirectory(third_party/inipp)
add_subdirectory(third_party/imgui)
add_subdirectory(third_party/nativefiledialog)
add_subdirectory(third_party/libromfs)

set(SOURCE_FILES
  source/helpers/imgui.cpp
  source/helpers/lang.cpp
  source/helpers/nfd.cpp
  source/helpers/utils.cpp
  source/views/view.cpp
  source/views/command_palette.cpp
  source/views/context_menu.cpp
  source/views/debug.cpp
  source/views/about.cpp
  source/views/quickview.cpp
  source/views/settings.cpp
  source/theme.cpp
  source/config.cpp
  source/mpv.cpp
  source/player.cpp
  source/window.cpp
  source/main.cpp
)
set(INCLUDE_DIRS include ${MPV_INCLUDE_DIRS} ${GLFW_INCLUDE_DIRS} ${OPENGL_INCLUDE_DIR})
set(LINK_LIBS glad fmt::fmt natsort nlohmann_json::nlohmann_json inipp nfd imgui ${CMAKE_THREAD_LIBS_INIT} ${MPV_LIBRARIES} ${GLFW_LIBRARIES} ${LIBROMFS_LIBRARY})

if(WIN32)
  configure_file(${PROJECT_SOURCE_DIR}/resources/win32/app.rc.in ${PROJECT_BINARY_DIR}/app.rc @ONLY)
  list(APPEND SOURCE_FILES ${PROJECT_BINARY_DIR}/app.rc)
endif()

add_executable(${PROJECT_NAME} WIN32 ${SOURCE_FILES})
if(WIN32)
  set_target_properties(${PROJECT_NAME} PROPERTIES LINK_FLAGS "/ENTRY:mainCRTStartup")
endif()
target_include_directories(${PROJECT_NAME} PRIVATE ${INCLUDE_DIRS})
target_link_directories(${PROJECT_NAME} PRIVATE ${MPV_LIBRARY_DIRS})
target_link_libraries(${PROJECT_NAME} PRIVATE ${LINK_LIBS})
target_compile_definitions(${PROJECT_NAME} PRIVATE
  APP_VERSION="${GIT_VERSION}"
  $<$<BOOL:${USE_OPENGL_ES3}>:IMGUI_IMPL_OPENGL_ES3>
  $<$<BOOL:${USE_PATCHED_GLFW}>:GLFW_PATCHED>
)
if(USE_MPV_WIN_BUILD)
  add_dependencies(${PROJECT_NAME} mpv_dev)
endif()

if(CREATE_PACKAGE)
  include(CreateCpackPackage)
  prepare_package()
  create_package()
else()
  include(GNUInstallDirs)
  install(TARGETS ${PROJECT_NAME} RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR})
  if(UNIX AND NOT APPLE)
    install(FILES ${PROJECT_SOURCE_DIR}/resources/linux/implay.desktop DESTINATION ${CMAKE_INSTALL_DATAROOTDIR}/applications)
    install(FILES ${PROJECT_SOURCE_DIR}/resources/icon.png DESTINATION ${CMAKE_INSTALL_DATAROOTDIR}/pixmaps RENAME implay.png)
  endif()
endif()