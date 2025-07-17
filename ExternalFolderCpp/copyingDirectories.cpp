// Project/Properties/Language/C++ Language Standard => ISO C++17 Standard (/std:c++17)

#include "pch.h"
#include "copyingDirectories.h"
#include <iostream>
#include <filesystem> 
#include <Windows.h>
#include <string>
#include <string.h>

// copyOptions:
// 0 = entire folder including subfolders and all contents
// 1 = folder contents only (including subfolders)

// overwriteOptions:
// 0 = overwrite all
// 1 = overwrite only older files
// any other value = no action is performed

namespace fs = std::filesystem; // Project/Properties/Language/C++ Language Standard => ISO C++17 Standard (/std:c++17).

//c = 0
fs::path CopyEntireFolder(fs::path source)
{
	return fs::canonical(source); // will remove slash
}

//c = 1
fs::path CopyContentOnly(fs::path source)
{
	if (!source.empty() && source.generic_string().back() != '/')
	{
		source += '/';
	}

	return source;
}

//o = 0
auto OverwriteAll()
{
	return fs::copy_options::overwrite_existing | fs::copy_options::recursive;
}

//o = 1
auto OverwriteAllOldOnly()
{
	return fs::copy_options::update_existing | fs::copy_options::recursive;
}

auto SetOverwriteOptions(int o)
{
	switch (o)
	{
	case 0:
		return OverwriteAll();
	case 1:
		return OverwriteAllOldOnly();
	default:
		return fs::copy_options::none;//k tomu zatim nedojde
	}
}
//void CopyDirContent32(wchar_t* sourceString, wchar_t* targetParentString, int c, int o)  //32bit
void CopyDirContent64(wchar_t* sourceString, wchar_t* targetParentString, int c, int o)    //64bit
{
	if ((c == 0 || c == 1) && (o == 0 || o == 1))
	{
		std::filesystem::path source = sourceString;
		const std::filesystem::path targetParent = targetParentString;
		auto copyOptions = fs::copy_options::overwrite_existing | fs::copy_options::recursive;

		switch (c)
		{
		case 0:
			source = CopyEntireFolder(source);
			copyOptions = SetOverwriteOptions(o);
			break;
		case 1:
			source = CopyContentOnly(source);
			copyOptions = SetOverwriteOptions(o);
			break;
		default:
			copyOptions = fs::copy_options::none;
			break;
		}

		auto target = targetParent / source.filename(); // source.filename() returns "directory".

		fs::create_directories(target); // Recursively create target directory if not existing.
		fs::copy(source, target, copyOptions);
	}
}

/*
***options controlling copy_file() when the file already exists***
none	Report an error (default behavior)
skip_existing	Keep the existing file, without reporting an error.
overwrite_existing	Replace the existing file
update_existing	Replace the existing file only if it is older than the file being copied
	***options controlling the effects of copy() on subdirectories***
none	Skip subdirectories (default behavior)
recursive	Recursively copy subdirectories and their content //recursive = vcetne podadresaru
	***options controlling the effects of copy() on symbolic links***
none	Follow symlinks (default behavior)
copy_symlinks	Copy symlinks as symlinks, not as the files they point to
skip_symlinks	Ignore symlinks
	***options controlling the kind of copying copy() does***
none	Copy file content (default behavior)
directories_only	Copy the directory structure, but do not copy any non-directory files
create_symlinks	Instead of creating copies of files, create symlinks pointing to the originals. Note: the source path must be an absolute path unless the destination path is in the current directory.
create_hard_links	Instead of creating copies of files, create hardlinks that resolve to the same files as the originals
*/
//https://en.cppreference.com/w/cpp/filesystem/copy
//https://en.cppreference.com/w/cpp/filesystem/copy_options
