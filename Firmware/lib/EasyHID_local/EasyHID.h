/*
 Библиотека программного USB клавиатуры для Arduino (минимальная версия)
 GitHub: https://github.com/GyverLibs/EasyHID
 Минимальная версия: только Keyboard + Feature Report для команд

 MIT License
*/

#ifndef _EasyHID_h
#define _EasyHID_h

#include <Arduino.h>

#include "Codekeys.h"
#include "HIDPrivate.h"

#ifdef __cplusplus
extern "C" {
#endif
#ifdef __cplusplus
}
#endif

class EasyHID {
 public:
    void begin(void);
    void end(void);
    void tick(void);
    bool isConnected(void);
    bool isNumLock(void);
    bool isCapsLock(void);
    bool isScrollLock(void);
};

class KeyboardClass : public Print {
 public:
    void press(uint8_t k0, uint8_t k1 = 0, uint8_t k2 = 0, uint8_t k3 = 0, uint8_t k4 = 0);
    void click(uint8_t k0, uint8_t k1 = 0, uint8_t k2 = 0, uint8_t k3 = 0, uint8_t k4 = 0);
    void release(uint8_t k0, uint8_t k1 = 0, uint8_t k2 = 0, uint8_t k3 = 0, uint8_t k4 = 0);
    void releaseAll(void);
    virtual size_t write(uint8_t data);
    using Print::print;
    using Print::println;
    using Print::write;

 private:
    bool pushKey(uint8_t key);
    bool popKey(uint8_t key);
    uint8_t keyBuffer[5] = {0x00, 0x00, 0x00, 0x00, 0x00};
};

extern EasyHID HID;
extern KeyboardClass Keyboard;
#endif
