#include "pch.h"
#include <filesystem>
#include <Windows.h>
#include <string>

namespace fs = std::filesystem;

// Helper to move full folder (with same name)
fs::path PrepareMoveEntireFolder(fs::path source)
{
    return fs::canonical(source);
}

// Helper to move only contents
fs::path PrepareMoveContentOnly(fs::path source)
{
    if (!source.empty() && source.generic_string().back() != '/')
    {
        source += '/';
    }
    return source;
}

// Windows DLL export: Move directory contents
extern "C" __declspec(dllexport) int MoveDirContent64(
    wchar_t* sourceString,
    wchar_t* targetParentString,
    int      c) // c = 0 -> move entire folder, 1 -> move contents only
{
    try
    {
        if (c != 0 && c != 1)
            return 1; // invalid params

        fs::path source = sourceString;
        const fs::path parent = targetParentString;

        switch (c)
        {
        case 0:
            source = PrepareMoveEntireFolder(source);
            break;
        case 1:
            source = PrepareMoveContentOnly(source);
            break;
        }

        const fs::path target = (c == 0)
            ? parent / source.filename()
            : parent;

        fs::create_directories(target);

        for (const auto& entry : fs::recursive_directory_iterator(source))
        {
            const fs::path relativePath = fs::relative(entry.path(), source);
            const fs::path destPath = target / relativePath;

            if (fs::is_directory(entry))
            {
                fs::create_directories(destPath);
            }
            else if (fs::is_regular_file(entry))
            {
                fs::create_directories(destPath.parent_path());
                fs::rename(entry, destPath); // move (not copy)
            }
        }

        // Remove the original directory (only if we moved entire folder)
        if (c == 0)
            fs::remove_all(source);

        return 0; // Success
    }
    catch (...)
    {
        return 1; // Failure
    }
}
