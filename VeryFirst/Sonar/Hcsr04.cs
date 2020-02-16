using System;
using System.Threading;
using Windows.Devices.Gpio;

namespace VeryFirst.Device.Hcsr04
{
    public class Hcsr04 : IDisposable
    {
        private GpioPin _echo;
        private GpioPin _trigger;

        private long _lastMeasurment = 0;

        /// <summary>
        /// Gets the current distance in cm.
        /// </summary>
        public double Distance => GetDistance();

        /// <summary>
        /// Creates a new instance of the HC-SCR04 sonar.
        /// </summary>
        /// <param name="triggerPin">Trigger pulse input.</param>
        /// <param name="echoPin">Trigger pulse output.</param>
        public Hcsr04(int triggerPin, int echoPin)
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
            // Trigger input for 10uS to start ranging
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
            }

            var startTime = DateTime.UtcNow;
            _lastMeasurment = startTime.Ticks;

            // Wait until the pin is LOW again, (that marks the end of the pulse we are measuring)
            while (_echo.Read() == GpioPinValue.High)
            {
            }

            var justNow = DateTime.UtcNow;
            var elapsed = justNow - startTime;
            //var elapsedTicks = justNow.Ticks - startTime.Ticks;

            // distance = (time / 2) × velocity of sound (34300 cm/s)
            //double elapsedMilliseconds = elapsedTicks / 10000;
            var result = elapsed.TotalMilliseconds / 2.0 * 34.3;
            //double result = elapsedMilliseconds / 2.0 * 34.30;
            //Console.WriteLine($"Distance: {result.ToString("D2")} cm. ({elapsedMilliseconds} ms),  ({elapsedTicks} ticks)");
            return result;
        }

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
