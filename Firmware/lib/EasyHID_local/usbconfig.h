/*
 Стандартный usbcfg библиотеки v-usb
*/

#ifndef __usbconfig_h_included__
#define __usbconfig_h_included__

/* ATtiny48 / ATtiny88 */
#if (defined (__AVR_ATtiny48__) || defined (__AVR_ATtiny88__))
#define USB_CFG_IOPORTNAME D
#define USB_CFG_DMINUS_BIT 1
#define USB_CFG_DPLUS_BIT 2

#elif defined (__AVR_ATtiny167__)
#define USB_CFG_IOPORTNAME D
#define USB_CFG_DMINUS_BIT 3
#define USB_CFG_DPLUS_BIT 6

#elif (defined(__AVR_ATtiny85__) || defined(__AVR_ATtiny45__))
#define USB_CFG_IOPORTNAME B
#define USB_CFG_DMINUS_BIT 3
#define USB_CFG_DPLUS_BIT 4

#elif (defined (__AVR_ATmega48P__) || defined (__AVR_ATmega88P__) ||        defined (__AVR_ATmega8__) || defined (__AVR_ATmega168P__) || defined (__AVR_ATmega168__) ||        defined (__AVR_ATmega328P__) || defined (__AVR_ATmega328__))
#define USB_CFG_IOPORTNAME D
#define USB_CFG_DMINUS_BIT 4
#define USB_CFG_DPLUS_BIT 2

#endif

#define USB_CFG_CLOCK_KHZ (F_CPU/1000)
#define USB_CFG_CHECK_CRC 0

#define USB_CFG_HAVE_INTRIN_ENDPOINT 1
#define USB_CFG_HAVE_INTRIN_ENDPOINT3 0
#define USB_CFG_EP3_NUMBER 3
#define USB_CFG_IMPLEMENT_HALT 0
#define USB_CFG_SUPPRESS_INTR_CODE 0
#define USB_CFG_INTR_POLL_INTERVAL 10
#define USB_CFG_IS_SELF_POWERED 0
#define USB_CFG_MAX_BUS_POWER 100
#define USB_CFG_IMPLEMENT_FN_WRITE 1
#define USB_CFG_IMPLEMENT_FN_READ 0
#define USB_CFG_IMPLEMENT_FN_WRITEOUT 0
#define USB_CFG_HAVE_FLOWCONTROL 0
#define USB_CFG_DRIVER_FLASH_PAGE 0
#define USB_CFG_LONG_TRANSFERS 0
#define USB_CFG_HAVE_MEASURE_FRAME_LENGTH 1
#define USB_USE_FAST_CRC 0

#define USB_CFG_VENDOR_ID 0x81, 0x17
#define USB_CFG_DEVICE_ID 0xAB, 0x24
#define USB_CFG_DEVICE_VERSION 0x00, 0x01
#define USB_CFG_VENDOR_NAME 'G','y','v','e','r','L','i','b','s'
#define USB_CFG_VENDOR_NAME_LEN 9
#define USB_CFG_DEVICE_NAME 'E','a','s','y','H','I','D',' ','L','i','b','r','a','r','y'
#define USB_CFG_DEVICE_NAME_LEN 15
#define USB_CFG_DEVICE_CLASS 0x00
#define USB_CFG_DEVICE_SUBCLASS 0x00
#define USB_CFG_INTERFACE_CLASS 0x03
#define USB_CFG_INTERFACE_SUBCLASS 0x00
#define USB_CFG_INTERFACE_PROTOCOL 0x00
#define USB_CFG_HID_REPORT_DESCRIPTOR_LENGTH 83

#define USB_CFG_DESCR_PROPS_DEVICE 0
#define USB_CFG_DESCR_PROPS_CONFIGURATION 0
#define USB_CFG_DESCR_PROPS_STRINGS 0
#define USB_CFG_DESCR_PROPS_STRING_0 0
#define USB_CFG_DESCR_PROPS_STRING_VENDOR 0
#define USB_CFG_DESCR_PROPS_STRING_PRODUCT 0
#define USB_CFG_DESCR_PROPS_STRING_SERIAL_NUMBER 0
#define USB_CFG_DESCR_PROPS_HID 0
#define USB_CFG_DESCR_PROPS_HID_REPORT 0
#define USB_CFG_DESCR_PROPS_UNKNOWN 0

#define usbMsgPtr_t unsigned short

#if (defined(__AVR_ATtiny85__) || defined(__AVR_ATtiny45__))
#define USB_INTR_CFG PCMSK
#define USB_INTR_CFG_SET (1 << USB_CFG_DPLUS_BIT)
#define USB_INTR_CFG_CLR 0
#define USB_INTR_ENABLE GIMSK
#define USB_INTR_ENABLE_BIT PCIE
#define USB_INTR_PENDING GIFR
#define USB_INTR_PENDING_BIT PCIF
#define USB_INTR_VECTOR PCINT0_vect
#else
#define USB_INTR_VECTOR INT0_vect
#endif

#endif
