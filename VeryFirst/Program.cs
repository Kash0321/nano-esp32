using System;
using System.Threading;
using VeryFirst.Device.Hcsr04;
using Windows.Devices.Gpio;

namespace VeryFirst
{
    public class Program
    {
        public static void Main()
        {
            DistanceMonitoring();
        }

        private static void DistanceMonitoring()
        {
            Console.WriteLine("Hello Hcsr04 Sample!");

            GpioPin led = GpioController.GetDefault().OpenPin(18);
            led.SetDriveMode(GpioPinDriveMode.Output);

            using (var sonar = new Hcsr04(4, 5))
            {
                while (true)
                {
                    try
                    {
                        led.Toggle();
                        Console.WriteLine($"Distance: {sonar.Distance.ToString("D2")} cm");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}. {ex.ToString()}");
                    }
                    finally
                    {
                        Thread.Sleep(250);
                    }
                }
            }
        }
    }
}
