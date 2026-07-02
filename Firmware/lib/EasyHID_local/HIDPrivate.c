#include "HIDPrivate.h"

uint8_t report_buffer[8];
char usb_hasCommed = 0;
uint8_t idle_rate = 500 / 4;
uint8_t protocol_version = 0;
uint8_t led_state = 0;
volatile uint8_t hid_command = 0;

// ✅ НОВОЕ: флаг изменения состояния для отправки в ПК
volatile uint8_t state_changed = 0;

void usbReportSend(uint8_t sz) {
    while (1) {
        usbPoll();
        if (usbInterruptIsReady()) {
            usbSetInterrupt((uint8_t *)report_buffer, sz);
            break;
        }
    }
}

const PROGMEM char usbHidReportDescriptor[USB_CFG_HID_REPORT_DESCRIPTOR_LENGTH] = {
    0x05, 0x01,       // USAGE_PAGE (Generic Desktop)
    0x09, 0x06,       // USAGE (Keyboard)
    0xA1, 0x01,       // COLLECTION (Application)

    // Report ID 2: Keyboard
    0x85, REPID_KEYBOARD,
    0x75, 0x01, 0x95, 0x08, 0x05, 0x07, 0x19, 0xE0, 0x29, 0xE7,
    0x15, 0x00, 0x25, 0x01, 0x81, 0x02,
    0x95, 0x01, 0x75, 0x08, 0x81, 0x03,
    0x95, 0x05, 0x75, 0x01, 0x05, 0x08, 0x19, 0x01, 0x29, 0x05,
    0x91, 0x02, 0x95, 0x01, 0x75, 0x03, 0x91, 0x03,
    0x95, 0x05, 0x75, 0x08, 0x15, 0x00, 0x26, 0xA4, 0x00,
    0x05, 0x07, 0x19, 0x00, 0x2A, 0xA4, 0x00, 0x81, 0x00,

    // Report ID 5: Vendor Feature (Logical Collection)
    0x85, REPID_FEATURE,
    0xA1, 0x02,
    0x06, 0x00, 0xFF, 0x09, 0x01,
    0x75, 0x08, 0x95, 0x01,
    0xB1, 0x02,       // FEATURE (Data,Var,Abs)
    0xC0,             // END_COLLECTION (Logical)

    0xC0,             // END_COLLECTION (Application)
};

usbMsgLen_t usbFunctionSetup(uint8_t data[8]) {
    usb_hasCommed = 1;
    usbRequest_t *rq = (void *)data;

    if ((rq->bmRequestType & USBRQ_TYPE_MASK) != USBRQ_TYPE_CLASS)
        return 0;

    switch (rq->bRequest) {
        case USBRQ_HID_GET_IDLE:
            usbMsgPtr = &idle_rate;
            return 1;
        case USBRQ_HID_SET_IDLE:
            idle_rate = rq->wValue.bytes[1];
            return 0;
        case USBRQ_HID_GET_PROTOCOL:
            usbMsgPtr = &protocol_version;
            return 1;
        case USBRQ_HID_SET_PROTOCOL:
            protocol_version = rq->wValue.bytes[1];
            return 0;

        case USBRQ_HID_GET_REPORT:
            // ✅ НОВОЕ: Отдаём текущее состояние платы при GET_REPORT на Feature
            if (rq->wValue.bytes[0] == REPID_FEATURE) {
                report_buffer[0] = REPID_FEATURE;
                report_buffer[1] = state_changed;  // флаг изменения
                report_buffer[2] = 0;
                report_buffer[3] = 0;
                report_buffer[4] = 0;
                report_buffer[5] = 0;
                report_buffer[6] = 0;
                report_buffer[7] = 0;
                usbMsgPtr = (uint8_t *)&report_buffer;
                state_changed = 0;  // сбрасываем флаг после чтения
                return REPSIZE_FEATURE;
            }
            // Keyboard report
            if (rq->wValue.bytes[0] == REPID_KEYBOARD) {
                report_buffer[0] = REPID_KEYBOARD;
                report_buffer[1] = 0; report_buffer[2] = 0;
                report_buffer[3] = 0; report_buffer[4] = 0;
                report_buffer[5] = 0; report_buffer[6] = 0;
                report_buffer[7] = 0;
                usbMsgPtr = (uint8_t *)&report_buffer;
                return REPSIZE_KEYBOARD;
            }
            return 0;

        case USBRQ_HID_SET_REPORT:
            if (rq->wValue.bytes[0] == REPID_KEYBOARD && rq->wLength.word >= 2)
                return USB_NO_MSG;
            if (rq->wValue.bytes[0] == REPID_FEATURE && rq->wLength.word >= 1)
                return USB_NO_MSG;
            return 0;

        default:
            return 0;
    }
}

usbMsgLen_t usbFunctionWrite(uint8_t *data, uchar len) {
    if (len >= 2 && data[0] == REPID_KEYBOARD)
        led_state = data[1];

    if (len >= 2 && data[0] == REPID_FEATURE && data[1] != 0)
        hid_command = data[1];

    return 1;
    
}

void usbFunctionWriteOut(uint8_t *data, uchar len) {
    if (len >= 2 && data[0] == REPID_OUTPUT && data[1] != 0)
        hid_command = data[1];
}