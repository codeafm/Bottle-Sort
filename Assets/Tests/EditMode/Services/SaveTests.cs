using System.IO;
using BottleSort.Services;
using BottleSort.UI;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class SaveTests
    {
        string _directory;
        SaveService _service;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "BottleSortSaveTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            _service = new SaveService(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void MissingFile_GivesNewPlayer()
        {
            var data = _service.Load();
            Assert.AreEqual(0, data.coins);
            Assert.AreEqual(1, data.highestUnlocked);
            Assert.AreEqual(SaveService.FormatVersion, data.version);
        }

        [Test]
        public void Progress_SurvivesReload()
        {
            var data = _service.Load();
            data.RecordWin(1, 3);
            data.RecordWin(2, 2);
            Economy.Grant(data, new RewardBundle(120, undo: 2, hints: 1, bottles: 1));
            data.giftDate = "2026-10-17";
            data.calendarDay = 4;
            data.owned.Add("glass");
            data.bottleSkin = "glass";
            data.theme = "violet";
            data.musicOn = false;
            data.chapterChests.Add(1);
            data.Save();

            var loaded = new SaveService(_directory).Load();
            Assert.AreEqual(120, loaded.coins);
            Assert.AreEqual(3, loaded.highestUnlocked);
            Assert.AreEqual(5, loaded.TotalStars);
            Assert.AreEqual(2, loaded.undoCount);
            Assert.AreEqual(1, loaded.hintCount);
            Assert.AreEqual(1, loaded.extraBottleCount);
            Assert.AreEqual("2026-10-17", loaded.giftDate);
            Assert.AreEqual(4, loaded.calendarDay);
            Assert.IsTrue(loaded.Owns("glass"));
            Assert.AreEqual("glass", loaded.bottleSkin);
            Assert.AreEqual("violet", loaded.theme);
            Assert.IsFalse(loaded.musicOn);
            CollectionAssert.Contains(loaded.chapterChests, 1);
        }

        [Test]
        public void SecondWrite_KeepsBackupAndNoTempFile()
        {
            var data = _service.Load();
            data.AddCoins(10);
            data.AddCoins(20);
            Assert.IsTrue(File.Exists(_service.Path));
            Assert.IsTrue(File.Exists(SaveStore.BackupPath(_service.Path)));
            Assert.IsFalse(File.Exists(SaveStore.TempPath(_service.Path)));
        }

        [Test]
        public void BrokenMainFile_FallsBackToBackup()
        {
            var data = _service.Load();
            data.AddCoins(10);
            data.AddCoins(20);
            File.WriteAllText(_service.Path, "{\"coins\":99,\"highestUnl");

            var loaded = new SaveService(_directory).Load();
            Assert.AreEqual(10, loaded.coins);
        }

        [Test]
        public void ZeroFilledFile_StartsFresh()
        {
            File.WriteAllBytes(_service.Path, new byte[64]);
            var loaded = _service.Load();
            Assert.AreEqual(0, loaded.coins);
            Assert.AreEqual(1, loaded.highestUnlocked);
        }

        [Test]
        public void OldFormat_GetsDefaultsForNewFields()
        {
            // Файл временного формата ui (версия 1): ни дат наград, ни колеса, ни громкости.
            File.WriteAllText(_service.Path,
                "{\"version\":1,\"coins\":340,\"highestUnlocked\":12,\"stars\":[3,3,3,2],\"undoCount\":4," +
                "\"bottleSkin\":\"classic\",\"theme\":\"auto\",\"owned\":[\"classic\",\"navy\"],\"musicOn\":true}");

            var loaded = _service.Load();
            Assert.AreEqual(SaveService.FormatVersion, loaded.version);
            Assert.AreEqual(340, loaded.coins);
            Assert.AreEqual(12, loaded.highestUnlocked);
            Assert.AreEqual(4, loaded.undoCount);
            Assert.AreEqual(1, loaded.calendarDay);
            Assert.AreEqual(1, loaded.wheelSpins);
            Assert.AreEqual(0.6f, loaded.musicVolume, 1e-4f);
            Assert.IsNotNull(loaded.chapterChests);
            Assert.IsTrue(loaded.soundsOn);
        }

        [Test]
        public void BrokenValues_AreRepaired()
        {
            File.WriteAllText(_service.Path, "{\"coins\":-50,\"highestUnlocked\":0,\"calendarDay\":12,\"owned\":[],\"bottleSkin\":\"\"}");
            var loaded = _service.Load();
            Assert.AreEqual(0, loaded.coins);
            Assert.AreEqual(1, loaded.highestUnlocked);
            Assert.AreEqual(7, loaded.calendarDay);
            Assert.IsTrue(loaded.Owns("classic"));
            Assert.AreEqual("classic", loaded.bottleSkin);
        }

        [Test]
        public void Delete_RemovesEverything()
        {
            var data = _service.Load();
            data.AddCoins(5);
            data.AddCoins(5);
            _service.Delete();
            Assert.IsFalse(File.Exists(_service.Path));
            Assert.IsFalse(File.Exists(SaveStore.BackupPath(_service.Path)));
            Assert.AreEqual(0, _service.Load().coins);
        }
    }
}
