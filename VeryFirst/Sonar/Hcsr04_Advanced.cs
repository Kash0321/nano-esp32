using nanoFramework.Hardware.Esp32;
using System;
using System.Threading;
using Windows.Devices.Gpio;

namespace VeryFirst.Device.Hcsr04
{
    /// <summary>
    /// HC-SR04 - Ultrasonic Ranging Module
    /// </summary>
    public class Hcsr04_Advanced : IDisposable
    {
        private GpioPin _echo;
        private GpioPin _trigger;

        private long _lastMeasurment = 0;

        /// <summary>
        /// Gets the current distance in cm.9
        /// </summary>
        public double Distance => GetDistance();

        /// <summary>
        /// Creates a new instance of the HC-SCR04 sonar.
        /// </summary>
        /// <param name="triggerPin">Trigger pulse input.</param>
        /// <param name="echoPin">Trigger pulse output.</param>
        public Hcsr04_Advanced(int triggerPin, int echoPin)
        {
            _echo = GpioController.GetDefault().OpenPin(echoPin);
            _echo.SetDriveMode(GpioPinDriveMode.InputPullDown);

            _trigger = GpioController.GetDefault().OpenPin(triggerPin);
            _trigger.SetDriveMode(GpioPinDriveMode.Output);

            _trigger.Write(GpioPinValue.Low);

            // Call Read once to make sure method is JITted
            // Too long JITting is causing that initial echo pulse is frequently missed on the first run
            // which would cause unnecessary retry
            _echo.Read();
        }

        /// <summary>
        /// Gets the current distance in cm.
        /// </summary>
        private double GetDistance()
        {
            // Retry at most 10 times.
            // Try method will fail when context switch occurs in the wrong moment
            // or something else (i.e. JIT, extra workload) causes extra delay.
            // Other situation is when distance is changing rapidly (i.e. moving hand in front of the sensor)
            // which is causing invalid readings.
            for (int i = 0; i < 10; i++)
            {
                if (TryGetDistance(out double result))
                {
                    return result;
                }
            }

            throw new InvalidOperationException("Could not get reading from the sensor");
        }

        private bool TryGetDistance(out double result)
        {
            // Time when we give up on looping and declare that reading failed
            // 100ms was chosen because max measurement time for this sensor is around 24ms for 400cm
            // additionally we need to account 60ms max delay.
            // Rounding this up to a 100 in case of a context switch.
            long hangTicks = DateTime.UtcNow.Ticks + 100;

            // Measurements should be 60ms apart, in order to prevent trigger signal mixing with echo signal
            // ref https://components101.com/sites/default/files/component_datasheet/HCSR04%20Datasheet.pdf
            while (DateTime.UtcNow.Ticks - _lastMeasurment < 60)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(DateTime.UtcNow.Ticks - _lastMeasurment));
            }

            // Trigger input for 10uS to start ranging
            _trigger.Write(GpioPinValue.High);
            Thread.Sleep(TimeSpan.FromTicks(100));
            _trigger.Write(GpioPinValue.Low);

            // Wait until the echo pin is HIGH (that marks the beginning of the pulse length we want to measure)
            while (_echo.Read() == GpioPinValue.Low)
            {
                if (DateTime.UtcNow.Ticks - hangTicks > 0)
                {
                    result = default;
                    return false;
                }
            }

            var startTime = DateTime.UtcNow;
            var startUSecs = HighResTimer.GetCurrent();
            _lastMeasurment = startTime.Ticks;

            // Wait until the pin is LOW again, (that marks the end of the pulse we are measuring)
            while (_echo.Read() == GpioPinValue.High)
            {
                if (DateTime.UtcNow.Ticks - hangTicks > 0)
                {
                    result = default;
                    return false;
                }
            }

            var justNowUSecs = HighResTimer.GetCurrent();

            // distance = (time / 2) × velocity of sound (34300 cm/s)
            double time = justNowUSecs - startUSecs;
            time /= 1000;
            result = time / 2.0 * 34.3;

            if (result > 400)
            {
                // result is more than sensor supports
                // something went wrong
                result = default;
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_echo != null)
            {
                _echo.Dispose();
                _echo = null;
                _trigger.Dispose();
                _trigger = null;
            }
        }
    }
}
