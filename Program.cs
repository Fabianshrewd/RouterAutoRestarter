using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace RouterAutoRestarter
{
    class Program
    {
        static void Main(string[] args)
        {
            double desired_speed = -1;
            string IP_Adress = "";
            string admin_password = "";
            string model = "";

            //Output intruduction
            Console.WriteLine("This application restarts the router so lang that it logs in to the right LTE Tower.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            Console.WriteLine("\n");
            Console.WriteLine("Please enter your desired speed (1-10): ");
            desired_speed = Convert.ToDouble(Console.ReadLine());
            Console.WriteLine("Please enter the model:");
            model = Convert.ToString(Console.ReadLine());
            Console.WriteLine("Please enter the routers IP Address: ");
            IP_Adress = Convert.ToString(Console.ReadLine());
            Console.WriteLine("Please enter the Admin Password: ");
            admin_password = Convert.ToString(Console.ReadLine());

            //Check if target is online
            bool target_online = PingIP(IP_Adress);
            if (target_online == true)
            {
                bool goal_reached = false;
                while (goal_reached == false)
                {
                    //Check download speed
                    double download_speed = SpeedTest();

                    //Quit if the speed is alright
                    if (download_speed > desired_speed)
                    {
                        goal_reached = true;
                        break;
                    }

                    //Otherwise restart the router
                    RestartRouter(IP_Adress, admin_password, model);

                    //Return to the top of the loop if the router is pinggable
                    Thread.Sleep(60000);
                    while (PingIP(IP_Adress) == false)
                    {
                        Thread.Sleep(5000);
                    }
                }

                //Tell the user that the speed was found
                Console.WriteLine("Desired speed was found.");
            }
            else
            {
                //Not online, not possible to do task
                Console.WriteLine("Make sure you are connected to your wlan.");
            }

            //Keep the application opened
            Console.ReadLine();
        }

        static async void RestartRouter(string ip, string admin_password, string model)
        {
            //Start restarting the router
            Console.WriteLine("Restart router...");

            //Get an default chrome browser
            var browserFetcher = new BrowserFetcher();

            //Make sure it is possible to download progress und download the browser
            browserFetcher.DownloadProgressChanged += (sender, e) =>
            {
                Console.WriteLine($"Downloaded {e.BytesReceived}/{e.TotalBytesToReceive} bytes ({e.ProgressPercentage}%).");
            };
            await browserFetcher.DownloadAsync(BrowserFetcher.DefaultChromiumRevision);

            //Launch the Browser in headless mode
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = browserFetcher.GetExecutablePath(BrowserFetcher.DefaultChromiumRevision)
            });

            //Do naviagtion on the page
            if(model == "1")
            {
                var page = await browser.NewPageAsync();
                await page.GoToAsync("http://" + ip + "/html/index.html?noredirect");
                await page.TypeAsync("#login_password", admin_password);
                await page.EvaluateExpressionAsync("EMUI.LoginObjController.Login(0)");
                await page.WaitForNavigationAsync();
                await page.EvaluateExpressionAsync("EMUI.RebootController.rebootExe()");
            }
            if (model == "2")
            {
                //Wait for the page to appear
                Thread.Sleep(20000);

                var page = await browser.NewPageAsync();
                await page.GoToAsync("http://" + ip + "/html/home.html");
                await page.EvaluateExpressionAsync("showloginDialog()");
                await page.EvaluateExpressionAsync("document.getElementById('password').value = '" + admin_password + "'");
                await page.EvaluateExpressionAsync("document.getElementById('pop_login').click()");

                //Wait for the login to disappear
                Thread.Sleep(5000);

                await page.GoToAsync("http://" + ip + "/html/reboot.html");

                //Wait for the website to load
                Thread.Sleep(5000);

                await page.EvaluateExpressionAsync("do_reboot()");
                Console.WriteLine("Reboot command send...");

                //Wait, for this router it takes longer to get shutdown
                Thread.Sleep(10000);
            }

            //Wait before closing the browser
            Thread.Sleep(10000);

            //Close the browser
            await browser.CloseAsync();

            //Router is restarting
            Console.WriteLine("Router is beeing restarted");
        }

        static double SpeedTest()
        {
            //Set URL to download from
            Uri URL = new Uri("https://www.bamsoftware.com/hacks/zipbomb/zblg.zip");
            WebClient wc = new WebClient();

            //Starting speedtest
            Console.WriteLine("Starting speedtest...");

            //Download the file
            double starttime = Environment.TickCount;
            wc.DownloadFile(URL, @"speedtest.txt");
            double endtime = Environment.TickCount;

            //Calculate how many seconds it took and then Calculate the mb/seconds
            double secs = Math.Floor(endtime - starttime) / 1000;
            double mbsec = 10 / secs;

            //Show output
            Console.WriteLine("Completed Download: ");
            Console.WriteLine("Time: " + secs);
            Console.WriteLine("Average bandwith: " + mbsec);

            //Delete file
            System.IO.File.Delete(@"speedtest.txt");
            Console.WriteLine("File has been deleted.");

            //Return the download speed
            return mbsec;
        }

        static bool PingIP(string IP_Address)
        {
            while (1 == 1)
            {
                try
                {
                    //Ping target to see if it is up
                    Ping ping = new Ping();
                    PingReply pingresult = ping.Send(IP_Address);
                    if (pingresult.Status.ToString() == "Success")
                    {
                        Console.WriteLine(DateTime.UtcNow + ": Connection is up.");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine(DateTime.UtcNow + ": Connection is not up again...");
                        return false;

                    }
                }
                catch (Exception e)
                {
                    Thread.Sleep(5000);
                    Console.WriteLine(DateTime.UtcNow + ": Connection is not up again...");
                    string error = e.ToString();
                }
            }
        }
    }
}
