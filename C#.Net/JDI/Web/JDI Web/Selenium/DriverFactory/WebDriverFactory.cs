using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Epam.JDI.Core.Interfaces.Base;
using Epam.JDI.Core.Interfaces.Settings;
using Epam.JDI.Core.Settings;
using JDI_Web.Selenium.Elements.Base;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.IE;
using OpenQA.Selenium.Remote;
using static System.String;
using static Epam.JDI.Core.Settings.JDISettings;
using static JDI_Web.Properties.Settings;

namespace JDI_Web.Selenium.DriverFactory
{
    public enum RunTypes { Local, Remote }
    public enum DriverTypes { Chrome, Firefox, IE }
    public class WebDriverFactory : IDriver<IWebDriver>
    {
        private Dictionary<string, Func<IWebDriver>> Drivers { get; } = new Dictionary<string, Func<IWebDriver>>();
        private Dictionary<string, IWebDriver> RunDrivers { get; } = new Dictionary<string, IWebDriver>();

        private string _currentDriverName;

        public string CurrentDriverName
        {
            get
            {
                if (IsNullOrEmpty(_currentDriverName))
                {
                    _currentDriverName = _driverNamesDictionary[DriverTypes.Chrome];
                    RegisterLocalDriver(DriverTypes.Chrome);
                }
                return _currentDriverName;
            }
            set { _currentDriverName = value; }
        }

        public string DriverPath { get; set; } = "C:/Selenium";
        public RunTypes RunType { get; set; } = RunTypes.Local;
        public HighlightSettings HighlightSettings = new HighlightSettings();
        public Func<IWebElement, bool> ElementSearchCriteria = el => el.Displayed;

        private readonly Dictionary<DriverTypes, string> _driverNamesDictionary = new Dictionary<DriverTypes, string>
        {
            {DriverTypes.Chrome, "chrome"},
            {DriverTypes.Firefox, "firefox"},
            {DriverTypes.IE, "internet explorer"}
        };
        private readonly Dictionary<DriverTypes, Func<string, IWebDriver>> _driversDictionary = new Dictionary<DriverTypes, Func<string, IWebDriver>>
        {
            {DriverTypes.Chrome, path => IsNullOrEmpty(path) ? new ChromeDriver() : new ChromeDriver(path)},
            {DriverTypes.Firefox, path => new FirefoxDriver()},
            {DriverTypes.IE, path => IsNullOrEmpty(path) ? new InternetExplorerDriver() : new InternetExplorerDriver(path)}
        };
        
        private string RegisterLocalDriver(DriverTypes driverType)
        {
            return RegisterDriver(GetDriverName(_driverNamesDictionary[driverType]), 
                () => WebDriverSettings(_driversDictionary[driverType](DriverPath)));
        }

        private string GetDriverName(string driverName)
        {
            if (!Drivers.ContainsKey(driverName))
                return driverName;
            string newName;
            var i = 1;
            do { newName = driverName + i++;
            } while (Drivers.ContainsKey(newName));
            return newName;
        }

        public string RegisterDriver(string driverName, Func<IWebDriver> driver)
        {
            if (Drivers.ContainsKey(driverName))
                throw Exception($"Can't register WebDriver {driverName}. Driver with the same name already registered");
            try
            {
                Drivers.Add(driverName, driver);
                CurrentDriverName = driverName;
            }
            catch
            {
                throw Exception($"Can't register WebDriver {driverName}.");
            }
            return driverName;
        }

        public string RegisterDriver(Func<IWebDriver> driver)
        {
            return RegisterDriver("Driver" + (Drivers.Count + 1), driver);
        }

        public IWebDriver GetDriver()
        {
            try
            {
                if (!IsNullOrEmpty(CurrentDriverName))
                    return GetDriver(CurrentDriverName);
                RegisterDriver(DriverTypes.Chrome);
                return GetDriver(DriverTypes.Chrome);
            }
            catch
            {
                throw new Exception(); // TODO
            }
        }

        public IWebDriver GetDriver(DriverTypes driverType)
        {
            return GetDriver(_driverNamesDictionary[driverType]);
        }
        public Func<IWebDriver, IWebDriver> WebDriverSettings = driver => {
            driver.Manage().Window.Maximize();
            driver.Manage().Timeouts().ImplicitlyWait(TimeSpan.FromSeconds(Timeouts.WaitElementSec));
            return driver;
        };

        public IWebDriver GetDriver(string driverName)
        {
            if (!Drivers.ContainsKey(driverName))
                throw new Exception($"Can't find driver with name {driverName}");
            try
            {
                if (!RunDrivers.ContainsKey(driverName))
                {
                    var resultDriver = Drivers[driverName]();
                    if (resultDriver == null)
                        throw new Exception($"Can't get Webdriver {driverName}. This Driver name is not registered");
                    RunDrivers.Add(driverName, resultDriver);
                }
                return RunDrivers[driverName];
            }
            catch
            {
                throw new Exception("Can't get driver.");
            }
        }
        
        public string RegisterDriver(string driverName)
        {
            try
            {
                var driverType = _driverNamesDictionary.FirstOrDefault(x => x.Value == driverName).Key;
                return RegisterLocalDriver(driverType);
            }
            catch {
                throw new Exception(); // TODO
            }
        }

        public string RegisterDriver(DriverTypes driverType)
        {
            switch (RunType)
            {
                case RunTypes.Local:
                    return RegisterLocalDriver(driverType);
                case RunTypes.Remote:
                    return RegisterRemoteDriver(driverType);
            }
            throw new Exception(); // TODO
        }

        private string RegisterRemoteDriver(DriverTypes driverType)
        {
            var capabilities = new DesiredCapabilities(new Dictionary<string, object>
            {
                {"browserName", _driverNamesDictionary[driverType]},
                {"version", Empty},
                {"javaScript", true}
            });

            return RegisterDriver("Remote_" + _driverNamesDictionary[driverType],
                () => new RemoteWebDriver(new Uri(Default.remote_url), capabilities));
        }

        public void SwitchToDriver(string driverName)
        {
            if (Drivers.ContainsKey(driverName))
                CurrentDriverName = driverName;
            else
                throw new Exception($"Can't switch to Webdriver {driverName}. This Driver name not registered");
        }

        public void ReopenDriver()
        {
            ReopenDriver(CurrentDriverName);
        }

        public void ReopenDriver(string driverName)
        {
            if (RunDrivers.ContainsKey(driverName))
            {
                RunDrivers[driverName].Close();
                RunDrivers.Remove(driverName);
            }
            if (Drivers.ContainsKey(driverName))
                GetDriver(); // TODO
        }

        public void Close()
        {
            foreach (var driver in RunDrivers)
                driver.Value.Quit();
            RunDrivers.Clear();
        }

        public void SetRunType(string runType)
        {
            switch (runType)
            {
                case "local" : RunType = RunTypes.Local; return;
                case "remote" : RunType = RunTypes.Remote; return;
            }
            RunType = RunTypes.Local;
        }

        public bool HasDrivers()
        {
            return Drivers.Any();
        }

        public bool HasRunDrivers()
        {
            return RunDrivers.Any();
        }
        
        public void Highlight(IElement element)
        {
            Highlight(element, HighlightSettings);
        }

        public void Highlight(IElement element, HighlightSettings highlightSettings)
        {
            if (highlightSettings == null)
                highlightSettings = new HighlightSettings();
            var orig = ((WebElement) element).GetWebElement().GetAttribute("style");
            element.SetAttribute("style",
                $"border: 3px solid {highlightSettings.FrameColor}; background-color: {highlightSettings.BgColor};");
            Thread.Sleep(highlightSettings.TimeoutInSec * 1000);
            element.SetAttribute("style", orig);
        }
        
    }
}