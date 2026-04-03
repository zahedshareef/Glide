using System;
using System.Collections.Generic;
using Xunit;
using Glide.Settings;
using Glide.Settings.Models;

namespace Glide.Tests
{
    public class ProfileResolverTests
    {
        [Fact]
        public void Resolve_ReturnsNull_WhenGloballyDisabled()
        {
            var manager = new SettingsManager();
            manager.Current.IsEnabled = false;

            var resolver = new ProfileResolver(manager);
            var result = resolver.Resolve("notepad");

            Assert.Null(result);
        }

        [Fact]
        public void Resolve_BlacklistMode_BlocksListedApp()
        {
            var manager = new SettingsManager();
            manager.Current.IsEnabled = true;
            manager.Current.FilterMode = FilterMode.Blacklist;
            manager.Current.FilterList = new List<string> { "notepad" };

            var resolver = new ProfileResolver(manager);
            
            var resultNotepad = resolver.Resolve("notepad");
            var resultOther = resolver.Resolve("chrome");

            Assert.Null(resultNotepad);
            Assert.NotNull(resultOther);
        }

        [Fact]
        public void Resolve_WhitelistMode_AllowsOnlyListedApp()
        {
            var manager = new SettingsManager();
            manager.Current.IsEnabled = true;
            manager.Current.FilterMode = FilterMode.Whitelist;
            manager.Current.FilterList = new List<string> { "notepad" };

            var resolver = new ProfileResolver(manager);
            
            var resultNotepad = resolver.Resolve("notepad");
            var resultOther = resolver.Resolve("chrome");

            Assert.NotNull(resultNotepad);
            Assert.Null(resultOther);
        }

        [Fact]
        public void Resolve_AppliesAppOverrides_OverGlobalProfile()
        {
            var manager = new SettingsManager();
            manager.Current.IsEnabled = true;
            manager.Current.FilterMode = FilterMode.Blacklist;
            manager.Current.FilterList = new List<string>();

            // Global profile defaults
            manager.Current.GlobalProfile.StepSize = 100;
            manager.Current.GlobalProfile.AnimationTime = 500;

            // App override
            manager.Current.AppOverrides["chrome"] = new ScrollProfile
            {
                StepSize = 250
            };

            var resolver = new ProfileResolver(manager);
            
            var chromeProfile = resolver.Resolve("chrome");
            var defaultProfile = resolver.Resolve("notepad");

            Assert.NotNull(chromeProfile);
            Assert.NotNull(defaultProfile);
            
            // Expected overrides
            Assert.Equal(250, chromeProfile.StepSize);
            Assert.Equal(500, chromeProfile.AnimationTime); // Falls back to global
            
            // Expected global
            Assert.Equal(100, defaultProfile.StepSize);
            Assert.Equal(500, defaultProfile.AnimationTime);
        }

        [Fact]
        public void Resolve_IsCaseInsensitive()
        {
            var manager = new SettingsManager();
            manager.Current.IsEnabled = true;
            manager.Current.FilterMode = FilterMode.Whitelist;
            manager.Current.FilterList = new List<string> { "NoTePaD" }; // Mixed case

            var resolver = new ProfileResolver(manager);
            
            // Should match despite case differences
            var result1 = resolver.Resolve("notepad");
            var result2 = resolver.Resolve("NOTEPAD");

            Assert.NotNull(result1);
            Assert.NotNull(result2);
        }
    }
}
