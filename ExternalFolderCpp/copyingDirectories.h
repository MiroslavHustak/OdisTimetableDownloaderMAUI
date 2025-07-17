#pragma once

#ifdef __cplusplus
extern "C" {
#endif

	// Define DLL export macro
#ifdef _WIN32
#define DLLDIR __declspec(dllexport)
#else
#define DLLDIR
#endif

// Callback function type definition
//typedef void (*ProgressCallback)(double bytesCopied, double totalSize);

// Function declaration
DLLDIR void CopyDirContent64(wchar_t* sourceString, wchar_t* targetParentString, int c, int o);

#ifdef __cplusplus
}
#endif

/*
#pragma once

#include <exception>
#include <filesystem> 
#include <iostream>
#include <stdio.h>

// Project/Properties/Language/C++ Language Standard => ISO C++17 Standard (/std:c++17)

namespace fs = std::filesystem; 

using namespace std;

#ifdef DLLDIR_EX
#define DLLDIR  __declspec(dllexport)   // export DLL information
#else
#define DLLDIR  __declspec(dllimport)   // import DLL information
#endif 

//extern "C" DLLDIR void CopyDirContent32(wchar_t* sourceString, wchar_t* targetParentString, int c, int o);
extern "C" DLLDIR void CopyDirContent64(wchar_t* sourceString, wchar_t* targetParentString, int c, int o);
*/