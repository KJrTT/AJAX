namespace Домашнее_задание_11._09._2026.Services
{
    public class AppLifetimeService
    {
        public AppLifetimeService(IHostApplicationLifetime lifetime)
        {
            lifetime.ApplicationStarted.Register(() =>
                Console.WriteLine("🟢 [LIFETIME] ApplicationStarted"));

            lifetime.ApplicationStopping.Register(() =>
                Console.WriteLine("🟡 [LIFETIME] ApplicationStopping"));

            lifetime.ApplicationStopped.Register(() =>
                Console.WriteLine("🔴 [LIFETIME] ApplicationStopped"));
        }
    }
}
