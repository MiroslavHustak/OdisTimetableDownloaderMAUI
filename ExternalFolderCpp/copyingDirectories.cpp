// Project/Properties/Language/C++ Language Standard => ISO C++17 Standard (/std:c++17)
// 
// git add ExternalFolderCpp //adresar musi byt v adresarovem systemu startovaciho projektu (nejdriv tam ale presun soubory z CppHelpers
// git commit -m "Add move"
// git push origin master

#include "pch.h"
#include "copyingDirectories.h"
#include <filesystem>
#include <Windows.h>
#include <string>

namespace fs = std::filesystem;

fs::path CopyEntireFolder(fs::path source)
{
    return fs::canonical(source);
}

fs::path CopyContentOnly(fs::path source)
{
    if (!source.empty() && source.generic_string().back() != '/')
    {
        source += '/';
    }
    return source;
}

auto OverwriteAll()
{
    return fs::copy_options::overwrite_existing | fs::copy_options::recursive;
}

auto OverwriteAllOldOnly()
{
    return fs::copy_options::update_existing | fs::copy_options::recursive;
}

auto SetOverwriteOptions(int o)
{
    switch (o)
    {
    case 0: return OverwriteAll();
    case 1: return OverwriteAllOldOnly();
    default: return fs::copy_options::none;
    }
}

extern "C" __declspec(dllexport) int CopyDirContent64(
    wchar_t* sourceString,
    wchar_t* targetParentString,
    int      c,
    int      o)
{
    try
    {
        if ((c == 0 || c == 1) && (o == 0 || o == 1))
        {
            fs::path source = sourceString;
            const fs::path parent = targetParentString;
            auto copyOptions = SetOverwriteOptions(o);

            switch (c)
            {
            case 0:
                source = CopyEntireFolder(source);
                break;
            case 1:
                source = CopyContentOnly(source);
                break;
            }

            const fs::path target = parent / source.filename();
            fs::create_directories(target);

            for (const auto& entry : fs::recursive_directory_iterator(source))
            {
                if (fs::is_regular_file(entry))
                {
                    const fs::path destFile = target / fs::relative(entry.path(), source);
                    fs::create_directories(destFile.parent_path());
                    fs::copy_file(entry, destFile, copyOptions);
                }
            }

            return 0; // Success
        }

        return 1; // Invalid parameters
    }
    catch (const fs::filesystem_error&) // chytani exn pomoci zabaleni extern v F# do try-with je sice teoreticky mozne, ale slozite
    {
        return 1; // Failure due to filesystem error
    }
    catch (...)
    {
        return 1; // Catch-all for unexpected failures
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