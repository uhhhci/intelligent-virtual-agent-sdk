using NUnit.Framework;
using UnityEngine;
using IVH.Core.Utils.Logging;

namespace IVH.Core.Tests.Logging
{
    /// <summary>
    /// Unit tests for the IVALogger level filter and config loading.
    /// These are EditMode tests — no Unity runtime required.
    /// </summary>
    public class IVALoggerTests
    {
        private IVALogConfig _testConfig;

        [SetUp]
        public void SetUp()
        {
            _testConfig = ScriptableObject.CreateInstance<IVALogConfig>();
            IVALogger.SetConfig(_testConfig);
            IVALogger.SetMinLevel(null);
        }

        [TearDown]
        public void TearDown()
        {
            IVALogger.SetConfig(null);
            IVALogger.SetMinLevel(null);
            if (_testConfig != null) Object.DestroyImmediate(_testConfig);
        }

        [Test]
        public void IsEnabled_RespectsConfigMinLevel()
        {
            _testConfig.minLevel = LogLevel.Warn;
            IVALogger.SetConfig(_testConfig);

            Assert.IsFalse(IVALogger.IsEnabled(LogLevel.Trace));
            Assert.IsFalse(IVALogger.IsEnabled(LogLevel.Debug));
            Assert.IsFalse(IVALogger.IsEnabled(LogLevel.Info));
            Assert.IsTrue(IVALogger.IsEnabled(LogLevel.Warn));
            Assert.IsTrue(IVALogger.IsEnabled(LogLevel.Error));
        }

        [Test]
        public void SetMinLevel_OverridesConfig()
        {
            _testConfig.minLevel = LogLevel.Info;
            IVALogger.SetConfig(_testConfig);
            IVALogger.SetMinLevel(LogLevel.Error);

            Assert.IsFalse(IVALogger.IsEnabled(LogLevel.Warn));
            Assert.IsTrue(IVALogger.IsEnabled(LogLevel.Error));
        }

        [Test]
        public void SetMinLevel_Null_FallsBackToConfig()
        {
            _testConfig.minLevel = LogLevel.Debug;
            IVALogger.SetConfig(_testConfig);
            IVALogger.SetMinLevel(LogLevel.Error);
            IVALogger.SetMinLevel(null);

            Assert.AreEqual(LogLevel.Debug, IVALogger.EffectiveMinLevel);
        }

        [Test]
        public void NoConfig_DefaultsToInfo()
        {
            IVALogger.SetConfig(null);

            Assert.AreEqual(LogLevel.Info, IVALogger.EffectiveMinLevel);
            Assert.IsTrue(IVALogger.IsEnabled(LogLevel.Info));
            Assert.IsFalse(IVALogger.IsEnabled(LogLevel.Debug));
        }

        [Test]
        public void OffLevel_SuppressesEverything()
        {
            _testConfig.minLevel = LogLevel.Off;
            IVALogger.SetConfig(_testConfig);

            Assert.IsFalse(IVALogger.IsEnabled(LogLevel.Trace));
            Assert.IsFalse(IVALogger.IsEnabled(LogLevel.Debug));
            Assert.IsFalse(IVALogger.IsEnabled(LogLevel.Info));
            Assert.IsFalse(IVALogger.IsEnabled(LogLevel.Warn));
            Assert.IsFalse(IVALogger.IsEnabled(LogLevel.Error));
        }

        [Test]
        public void LogLevel_OrderingIsCorrect()
        {
            Assert.Less((int)LogLevel.Trace, (int)LogLevel.Debug);
            Assert.Less((int)LogLevel.Debug, (int)LogLevel.Info);
            Assert.Less((int)LogLevel.Info, (int)LogLevel.Warn);
            Assert.Less((int)LogLevel.Warn, (int)LogLevel.Error);
            Assert.Less((int)LogLevel.Error, (int)LogLevel.Off);
        }
    }
}
