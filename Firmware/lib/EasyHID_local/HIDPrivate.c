#include "HIDPrivate.h"

uint8_t report_buffer[8];
char usb_hasCommed = 0;
uint8_t idle_rate = 500 / 4;
uint8_t protocol_version = 0;
uint8_t led_state = 0;
volatile uint8_t hid_command = 0;

void usbReportSend(uint8_t sz) {
    while (1) {
        usbPoll();
        if (usbInterruptIsReady()) {
            usbSetInterrupt((uint8_t *)report_buffer, sz);
            break;
        }
    }
}

// HID Report Descriptor: ОДНА Application collection (Keyboard)
// Feature Report вложен в Logical Collection внутри неё
// Длина: 83 байта
const PROGMEM char usbHidReportDescriptor[USB_CFG_HID_REPORT_DESCRIPTOR_LENGTH] = {
    // ===== Application Collection: Keyboard =====
    0x05, 0x01,       // USAGE_PAGE (Generic Desktop)
    0x09, 0x06,       // USAGE (Keyboard)
    0xA1, 0x01,       // COLLECTION (Application)

    // --- Report ID 2: Keyboard Input + LED Output ---
    0x85, REPID_KEYBOARD, // REPORT_ID (2)

    // Modifier byte (Input)
    0x75, 0x01,       // REPORT_SIZE (1)
    0x95, 0x08,       // REPORT_COUNT (8)
    0x05, 0x07,       // USAGE_PAGE (Keyboard)
    0x19, 0xE0,       // USAGE_MINIMUM (224)
    0x29, 0xE7,       // USAGE_MAXIMUM (231)
    0x15, 0x00,       // LOGICAL_MINIMUM (0)
    0x25, 0x01,       // LOGICAL_MAXIMUM (1)
    0x81, 0x02,       // INPUT (Data,Var,Abs)

    // Reserved byte (Input)
    0x95, 0x01,       // REPORT_COUNT (1)
    0x75, 0x08,       // REPORT_SIZE (8)
    0x81, 0x03,       // INPUT (Cnst,Var,Abs)

    // LED Output (5 bits + 3 bits padding)
    0x95, 0x05,       // REPORT_COUNT (5)
    0x75, 0x01,       // REPORT_SIZE (1)
    0x05, 0x08,       // USAGE_PAGE (LEDs)
    0x19, 0x01,       // USAGE_MINIMUM (Num Lock)
    0x29, 0x05,       // USAGE_MAXIMUM (Kana)
    0x91, 0x02,       // OUTPUT (Data,Var,Abs)
    0x95, 0x01,       // REPORT_COUNT (1)
    0x75, 0x03,       // REPORT_SIZE (3)
    0x91, 0x03,       // OUTPUT (Cnst,Var,Abs)

    // Keycodes (Input)
    0x95, 0x05,       // REPORT_COUNT (5)
    0x75, 0x08,       // REPORT_SIZE (8)
    0x15, 0x00,       // LOGICAL_MINIMUM (0)
    0x26, 0xA4, 0x00, // LOGICAL_MAXIMUM (164)
    0x05, 0x07,       // USAGE_PAGE (Keyboard)
    0x19, 0x00,       // USAGE_MINIMUM (0)
    0x2A, 0xA4, 0x00, // USAGE_MAXIMUM (164)
    0x81, 0x00,       // INPUT (Data,Ary,Abs)

    // --- Report ID 5: Vendor Feature (Logical Collection) ---
    0x85, REPID_FEATURE, // REPORT_ID (5)
    0xA1, 0x02,       // COLLECTION (Logical) ← вложенная коллекция!
    0x06, 0x00, 0xFF, // USAGE_PAGE (Vendor Defined)
    0x09, 0x01,       // USAGE (Vendor Usage 1)
    0x75, 0x08,       // REPORT_SIZE (8)
    0x95, 0x01,       // REPORT_COUNT (1)
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
            usbMsgPtr = (uint8_t *)&report_buffer;
            report_buffer[0] = rq->wValue.bytes[0];
            report_buffer[1] = report_buffer[2] = report_buffer[3] =
                report_buffer[4] = report_buffer[5] = report_buffer[6] =
                report_buffer[7] = 0;
            if (rq->wValue.bytes[0] == REPID_KEYBOARD) return REPSIZE_KEYBOARD;
            if (rq->wValue.bytes[0] == REPID_FEATURE) return 1;
            return 8;
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
