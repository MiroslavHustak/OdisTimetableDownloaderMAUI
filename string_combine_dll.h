#ifndef STRING_COMBINE_DLL_H
#define STRING_COMBINE_DLL_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

	char* combine_strings(const uint16_t* s1, const uint16_t* s2);
	void free_string(char* ptr);

#ifdef __cplusplus
}
#endif

#endif // STRING_COMBINE_DLL_H
