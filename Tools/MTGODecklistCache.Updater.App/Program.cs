﻿using MTGODecklistCache.Updater.Model;
using MTGODecklistCache.Updater.MtgMelee;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MTGODecklistCache.Updater.App
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: MTGODecklistCache.Updater.App CACHE_FOLDER RAWDATA_FOLDER [START_DATE] [END_DATE]");
                return;
            }

            string cacheFolder = new DirectoryInfo(args[0]).FullName;
            string rawDataFolder = new DirectoryInfo(args[1]).FullName;

            DateTime startDate = DateTime.Now.AddDays(-7).ToUniversalTime().Date;
            if (args.Length > 2)
            {
                startDate = DateTime.Parse(args[2], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
            }
            DateTime? endDate = null;
            if (args.Length > 3)
            {
                endDate = DateTime.Parse(args[3], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
            }

            //// Updates MTGO cache folder
            UpdateFolder(cacheFolder, "mtgo.com",
                () => MTGODecklistCache.Updater.Mtgo.TournamentList.GetTournaments(startDate, endDate),
                t => MTGODecklistCache.Updater.Mtgo.TournamentLoader.GetTournamentDetails(t));

            // Updates ManaTraders cache folder
            UpdateFolder(cacheFolder, "manatraders.com",
                () => MTGODecklistCache.Updater.ManaTraders.TournamentList.GetTournaments(),
                t => MTGODecklistCache.Updater.ManaTraders.TournamentLoader.GetTournamentDetails(t));

            // Updates MagicGG cache folder
            UpdateFolder(cacheFolder, "magic.gg",
                () => MTGODecklistCache.Updater.Common.FolderTournamentList.GetTournaments<Tournament>(Path.Combine(rawDataFolder, "MagicGG")),
                t => MTGODecklistCache.Updater.MagicGG.TournamentLoader.GetTournamentDetails(t));

            // Updates MtgMelee cache folder
            UpdateFolder(cacheFolder, "mtgmelee.com",
                () => MTGODecklistCache.Updater.Common.FolderTournamentList.GetTournaments<MtgMeleeTournament>(Path.Combine(rawDataFolder, "MtgMelee")),
                t => MTGODecklistCache.Updater.MtgMelee.TournamentLoader.GetTournamentDetails(t));
        }

        static void UpdateFolder<TTournament, TCacheItem>(string cacheRootFolder, string provider, Func<TTournament[]> tournamentList, Func<TTournament, TCacheItem> tournamentLoader) where TTournament : Tournament
        {
            string cacheFolder = Path.Combine(cacheRootFolder, provider);

            Console.WriteLine($"Downloading tournament list for {provider}");
            foreach (var tournament in tournamentList().OrderBy(t => t.Date))
            {
                Console.WriteLine($"- Downloading tournament {tournament.JsonFile}");
                string targetFolder = Path.Combine(cacheFolder, tournament.Date.Year.ToString(), tournament.Date.Month.ToString("D2").ToString(), tournament.Date.Day.ToString("D2").ToString());
                if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

                string targetFile = Path.Combine(targetFolder, tournament.JsonFile);
                if (File.Exists(targetFile))
                {
                    Console.WriteLine($"-- Already downloaded, skipping");
                    continue;
                }

                var details = tournamentLoader(tournament);
                string contents = JsonConvert.SerializeObject(details, Formatting.Indented);

                File.WriteAllText(targetFile, contents);
            }
        }
    }
}
