#include "EasyHID.h"

#include <util/delay.h>

#include "Codekeys.h"

EasyHID HID;
KeyboardClass Keyboard;

void asciiToKey(uint8_t data, uint8_t isCaps, uint8_t* mod, uint8_t* key);

/*---------------------------------------Общий класс HID-----------------------------------*/
void EasyHID::begin(void) {
#if (defined(__AVR_ATtiny48__) || defined(__AVR_ATtiny88__))
    pinMode(1, INPUT);
    pinMode(2, INPUT);
#elif defined(__AVR_ATtiny167__)
    pinMode(3, INPUT);
    pinMode(4, INPUT);
#elif (defined(__AVR_ATtiny85__) || defined(__AVR_ATtiny45__))
    pinMode(3, INPUT);
    pinMode(4, INPUT);
#elif (defined(__AVR_ATmega48P__) || defined(__AVR_ATmega88P__) ||        defined(__AVR_ATmega168P__) || defined(__AVR_ATmega168__) ||        defined(__AVR_ATmega328P__) || defined(__AVR_ATmega328__))
    pinMode(4, INPUT);
    pinMode(2, INPUT);
#elif defined(__AVR_ATmega8__)
    DDRD &= ~(1 << PD2);
    PORTD &= ~(1 << PD2);
    DDRD &= ~(1 << PD4);
    PORTD &= ~(1 << PD4);
#endif

    cli();
    usbDeviceDisconnect();
    _delay_ms(250);
    usbDeviceConnect();
    usbInit();
    sei();
}

void EasyHID::end(void) {
    cli();
    usbDeviceDisconnect();
    sei();
}

void EasyHID::tick(void) {
    usbPoll();
}

bool EasyHID::isConnected(void) {
    return !!usb_hasCommed;
}

bool EasyHID::isNumLock(void) {
    return !!(led_state & LED_NUM_MSK);
}

bool EasyHID::isCapsLock(void) {
    return !!(led_state & LED_CAPS_MSK);
}

bool EasyHID::isScrollLock(void) {
    return !!(led_state & LED_SCROLL_MSK);
}

/*---------------------------------------Класс клавиатуры-------------------------------*/
void KeyboardClass::press(uint8_t k0, uint8_t k1, uint8_t k2, uint8_t k3, uint8_t k4) {
    bool flag = (pushKey(k0) | pushKey(k1) | pushKey(k2) | pushKey(k3) | pushKey(k4));
    if (!flag) return;
    uint8_t mod = 0x00;
    for (uint8_t i = 0; i < 5; i++) {
        if (keyBuffer[i] >= KEY_LEFT_CONTROL && keyBuffer[i] <= KEY_RIGHT_GUI) {
            mod |= (1 << (keyBuffer[i] - KEY_LEFT_CONTROL));
        }
    }
    report_buffer[0] = REPID_KEYBOARD;
    report_buffer[1] = mod;
    for (uint8_t i = 0; i < 5; i++) {
        report_buffer[i + 3] = keyBuffer[i];
    }
    usbReportSend(REPSIZE_KEYBOARD);
}

void KeyboardClass::release(uint8_t k0, uint8_t k1, uint8_t k2, uint8_t k3, uint8_t k4) {
    bool flag = (popKey(k0) | popKey(k1) | popKey(k2) | popKey(k3) | popKey(k4));
    if (!flag) return;
    uint8_t mod = 0x00;
    for (uint8_t i = 0; i < 5; i++) {
        if (keyBuffer[i] >= KEY_LEFT_CONTROL && keyBuffer[i] <= KEY_RIGHT_GUI) {
            mod |= (1 << (keyBuffer[i] - KEY_LEFT_CONTROL));
        }
    }
    report_buffer[0] = REPID_KEYBOARD;
    report_buffer[1] = mod;
    for (uint8_t i = 0; i < 5; i++) {
        report_buffer[i + 3] = keyBuffer[i];
    }
    usbReportSend(REPSIZE_KEYBOARD);
}

void KeyboardClass::releaseAll(void) {
    for (uint8_t i = 0; i < 5; i++) keyBuffer[i] = 0x00;
    report_buffer[0] = REPID_KEYBOARD;
    report_buffer[1] = 0;
    for (uint8_t i = 0; i < 5; i++) {
        report_buffer[i + 3] = keyBuffer[i];
    }
    usbReportSend(REPSIZE_KEYBOARD);
}

void KeyboardClass::click(uint8_t k0, uint8_t k1, uint8_t k2, uint8_t k3, uint8_t k4) {
    press(k0, k1, k2, k3, k4);
    release(k0, k1, k2, k3, k4);
}

bool KeyboardClass::pushKey(uint8_t key) {
    if (!key) return false;
    for (uint8_t i = 0; i < 5; i++) {
        if (key == keyBuffer[i]) return false;
    }
    for (uint8_t i = 0; i < 5; i++) {
        if (!keyBuffer[i]) {
            keyBuffer[i] = key;
            return true;
        }
    }
    return false;
}

bool KeyboardClass::popKey(uint8_t key) {
    if (!key) return false;
    for (uint8_t i = 0; i < 5; i++) {
        if (key == keyBuffer[i]) {
            keyBuffer[i] = 0;
            return true;
        }
    }
    return false;
}

size_t KeyboardClass::write(uint8_t data) {
    uint8_t modifier, keycode;
    asciiToKey(data, (bool)(led_state & LED_CAPS_MSK), &modifier, &keycode);
    report_buffer[0] = REPID_KEYBOARD;
    report_buffer[1] = modifier;
    report_buffer[2] = 0;
    report_buffer[3] = keycode;
    usbReportSend(REPSIZE_KEYBOARD);
    releaseAll();
    return 1;
}

#define MOD_SHIFT (KEY_MOD_LEFT_SHIFT)

void asciiToKey(uint8_t data, uint8_t isCaps, uint8_t* mod, uint8_t* key) {
    *key = 0x00;
    *mod = 0x00;
    if (data >= 'A' && data <= 'Z') {
        *key = KEY_A + (data - 'A');
        *mod = (isCaps ? 0 : MOD_SHIFT);
    } else if (data >= 'a' && data <= 'z') {
        *key = KEY_A + (data - 'a');
        *mod = (isCaps ? MOD_SHIFT : 0);
    } else if (data >= '0' && data <= '9') {
        *key = (data != '0' ? KEY_1 + (data - '1') : KEY_0);
    } else {
        switch (data) {
            case '!': *mod = MOD_SHIFT; *key = KEY_1; break;
            case '@': *mod = MOD_SHIFT; *key = KEY_2; break;
            case '#': *mod = MOD_SHIFT; *key = KEY_3; break;
            case '$': *mod = MOD_SHIFT; *key = KEY_4; break;
            case '%': *mod = MOD_SHIFT; *key = KEY_5; break;
            case '^': *mod = MOD_SHIFT; *key = KEY_6; break;
            case '&': *mod = MOD_SHIFT; *key = KEY_7; break;
            case '*': *mod = MOD_SHIFT; *key = KEY_8; break;
            case '(': *mod = MOD_SHIFT; *key = KEY_9; break;
            case ')': *mod = MOD_SHIFT; *key = KEY_0; break;
            case ' ': *key = KEY_SPACE; break;
            case '\t': *key = KEY_TAB; break;
            case '\n': *key = KEY_ENTER; break;
            case '_': *mod = MOD_SHIFT;
            case '-': *key = KEY_MINUS; break;
            case '+': *mod = MOD_SHIFT;
            case '=': *key = KEY_EQUAL; break;
            case '{': *mod = MOD_SHIFT;
            case '[': *key = KEY_SQBRAK_LEFT; break;
            case '}': *mod = MOD_SHIFT;
            case ']': *key = KEY_SQBRAK_RIGHT; break;
            case '<': *mod = MOD_SHIFT;
            case ',': *key = KEY_COMMA; break;
            case '>': *mod = MOD_SHIFT;
            case '.': *key = KEY_PERIOD; break;
            case '?': *mod = MOD_SHIFT;
            case '/': *key = KEY_SLASH; break;
            case '|': *mod = MOD_SHIFT;
            case '\\': *key = 0x31; break;
            case '"': *mod = MOD_SHIFT;
            case '\'': *key = 0x34; break;
            case ':': *mod = MOD_SHIFT;
            case ';': *key = 0x33; break;
            case '~': *mod = MOD_SHIFT;
            case '`': *key = 0x35; break;
        }
    }
}
