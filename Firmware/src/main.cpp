#include <EasyHID.h>

// Внешняя переменная из HIDPrivate.c — команда от ПК
// 0x01 = включить, 0x02 = выключить, 0x03 = переключить
extern volatile uint8_t hid_command;

uint32_t myTimer1;
uint32_t blinkTimer;
int timer;
const int LED_PIN = 5;
const int BUTTON_PIN = 4;
bool isActive = false;
bool isEmulating = false;
bool ledState = false;
int emulationStep = 0;
uint32_t stepStartTime;

// Антидребезг кнопки
bool buttonState = false;
bool lastButtonState = false;
bool lastStableButtonState = false;
unsigned long lastDebounceTime = 0;
const unsigned long debounceDelay = 50;

// ----------------------------------------------------------------
// Применить новое состояние isActive
// ----------------------------------------------------------------
void applyActive(bool newState) {
    isActive = newState;
    if (isActive) {
        digitalWrite(LED_PIN, HIGH);
        myTimer1 = millis();
        timer = random(20, 30);
    } else {
        isEmulating = false;
        emulationStep = 0;
        digitalWrite(LED_PIN, LOW);
    }
}

// ----------------------------------------------------------------
void setup()
{
    HID.begin();
    randomSeed(analogRead(0));
    myTimer1 = millis();
    blinkTimer = millis();
    pinMode(LED_PIN, OUTPUT);
    pinMode(BUTTON_PIN, INPUT_PULLUP);

    buttonState = !digitalRead(BUTTON_PIN);
    lastButtonState = buttonState;
    lastStableButtonState = buttonState;
}

// ----------------------------------------------------------------
void loop()
{
    // --- 1. Команда от ПК через HID Feature Report ---
    if (hid_command != 0) {
        uint8_t cmd = hid_command;
        hid_command = 0; // сбросить до обработки (прерывание безопасно)

        if (cmd == 0x01 && !isActive) applyActive(true);
        else if (cmd == 0x02 && isActive) applyActive(false);
        else if (cmd == 0x03) applyActive(!isActive);
    }

    // --- 2. Физическая кнопка с антидребезгом ---
    bool reading = !digitalRead(BUTTON_PIN);

    if (reading != lastButtonState)
        lastDebounceTime = millis();

    if ((millis() - lastDebounceTime) > debounceDelay) {
        if (reading != buttonState) {
            buttonState = reading;

            // Только фронт нажатия
            if (buttonState == HIGH && lastStableButtonState == LOW)
                applyActive(!isActive);

            lastStableButtonState = buttonState;
        }
    }
    lastButtonState = reading;

    // --- 3. Мигание светодиода во время эмуляции ---
    if (isEmulating) {
        if (millis() - blinkTimer >= 50) {
            blinkTimer = millis();
            ledState = !ledState;
            digitalWrite(LED_PIN, ledState);
        }
    }

    // --- 4. Машина состояний эмуляции нажатий ---
    if (isEmulating) {
        switch (emulationStep) {
            case 0:
                Keyboard.click(KEY_LEFT_CONTROL);
                stepStartTime = millis();
                emulationStep = 1;
                break;
            case 1:
                if (millis() - stepStartTime >= 1000) {
                    Keyboard.click(KEY_LEFT_SHIFT);
                    stepStartTime = millis();
                    emulationStep = 2;
                }
                break;
            case 2:
                if (millis() - stepStartTime >= 2000) {
                    Keyboard.click(KEY_LEFT_CONTROL);
                    stepStartTime = millis();
                    emulationStep = 3;
                }
                break;
            case 3:
                if (millis() - stepStartTime >= 1500) {
                    Keyboard.click(KEY_LEFT_SHIFT);
                    stepStartTime = millis();
                    emulationStep = 4;
                }
                break;
            case 4:
                if (millis() - stepStartTime >= 100) {
                    isEmulating = false;
                    emulationStep = 0;
                    digitalWrite(LED_PIN, HIGH); // постоянное свечение в активном режиме
                }
                break;
        }
    }

    // --- 5. Запуск эмуляции по случайному таймеру ---
    if (isActive && !isEmulating) {
        if (millis() - myTimer1 >= (uint32_t)(timer * 1000)) {
            myTimer1 = millis();
            timer = random(20, 30);
            isEmulating = true;
            emulationStep = 0;
            ledState = true;
            digitalWrite(LED_PIN, ledState);
            blinkTimer = millis();
        }
    }

    HID.tick(); // вызывать не реже каждых 10 мс!
}
