#ifndef _HIDPrivate_h
#define _HIDPrivate_h

#ifdef __cplusplus
extern "C" {
#endif

#include <avr/pgmspace.h>
#include <avr/interrupt.h>
#include <avr/io.h>
#include <string.h>
#include <util/delay.h>

#include "usbconfig.h"
#include "usbdrv/usbdrv.h"

extern char usb_hasCommed;
extern uint8_t led_state;
extern uint8_t report_buffer[8];
extern volatile uint8_t hid_command;
extern volatile uint8_t state_changed;  // ✅ флаг изменения состояния

void usbReportSend(uint8_t sz);

#define REPID_KEYBOARD  2
#define REPID_FEATURE   5

#define REPSIZE_KEYBOARD  8
#define REPSIZE_FEATURE   2  // ✅ ReportID + state
#define REPID_OUTPUT    1
void usbFunctionWriteOut(uint8_t *data, uchar len);

#ifdef __cplusplus
}
#endif


#endif
