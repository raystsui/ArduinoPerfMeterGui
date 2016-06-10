# ArduinoPerfMeterGui
This is a C# project with simple GUI for driving a Arduino-based device with few analog meters.

The C# program gathers CPU%, FREE_RAM_MB and DISK_BUSY% performance statistics, format and send to the meter device through the virtual COM port.

The meter device receive float numbers plus few keywords for handshaking from the host and drive the analog meters with PWM signal.

There are four matics in this implementation:
CORE0%
CORE1%
RAM_FREE_MB
PHY1_DISK_BUSY%

Beside RAM_FREE_MB is 'plot' in logarithm scale (calculation done in the Arduino program), other are in linear scale from 0% to 100%.

Also by the Arduino program, all signal pass a digital two-pole low-pass filter for a smoother, more sensible and 'enjoyable' meter motion.
