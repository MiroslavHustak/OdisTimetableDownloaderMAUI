#pragma once

#ifdef __cplusplus
extern "C" {
#endif

    // Move directory or its contents
    // Parameters:
    //   sourceString       → path to source directory
    //   targetParentString → path to destination *parent* directory
    //   c = 0              → move entire folder (creates subfolder)
    //   c = 1              → move only contents (flattens into parent)
    //
    // Returns:
    //   0 = success
    //   1 = error
    __declspec(dllexport) int MoveDirContent64(
        wchar_t* sourceString,
        wchar_t* targetParentString,
        int      c);

#ifdef __cplusplus
}
#endif
