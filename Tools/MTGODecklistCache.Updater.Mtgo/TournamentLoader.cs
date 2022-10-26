﻿using HtmlAgilityPack;
using MTGODecklistCache.Updater.Model;
using MTGODecklistCache.Updater.Tools;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;

namespace MTGODecklistCache.Updater.Mtgo
{
    public static class TournamentLoader
    {
        public static CacheItem GetTournamentDetails(Tournament tournament)
        {
            string htmlContent;
            using (WebClient client = new WebClient())
            {
                htmlContent = client.DownloadString(tournament.Uri);
            }

            var dataRow = htmlContent.Replace("\r", "").Split("\n").First(l => l.Trim().StartsWith("window.MTGO.decklists.data"));
            int cutStart = dataRow.IndexOf("{");

            var jsonData = dataRow.Substring(cutStart, dataRow.Length - cutStart - 1);

            dynamic json = JsonConvert.DeserializeObject(jsonData);

            string eventType = json.event_type;
            DateTime eventDate = json.date;

            var decks = new List<Deck>();
            foreach (var deck in json.decks)
            {
                Dictionary<string, int> mainboard = new Dictionary<string, int>();
                Dictionary<string, int> sideboard = new Dictionary<string, int>();
                string playerName = deck.player;

                foreach (var deckSection in deck.deck)
                {
                    bool isSb = deckSection.SB;
                    Dictionary<string, int> deckSectionItems = isSb ? sideboard : mainboard;

                    foreach (var card in deckSection.DECK_CARDS)
                    {
                        string name = card.CARD_ATTRIBUTES.NAME;
                        int quantity = card.Quantity;

                        // JSON may contain multiple entries for the same card, likely if they come from different sets
                        if (!deckSectionItems.ContainsKey(name)) deckSectionItems.Add(name, 0);
                        deckSectionItems[name] += quantity;
                    }
                }

                decks.Add(new Deck()
                {
                    AnchorUri = new Uri($"{tournament.Uri.ToString()}#deck_{playerName}"),
                    Date = eventDate,
                    Player = playerName,
                    Mainboard = mainboard.Select(k => new DeckItem() { CardName = k.Key, Count = k.Value }).ToArray(),
                    Sideboard = sideboard.Select(k => new DeckItem() { CardName = k.Key, Count = k.Value }).ToArray(),
                    Result = eventType == "league" ? "5-0" : ""
                });
            }

            return new CacheItem()
            {
                Bracket = null,
                Standings = null,
                Rounds = null,
                Decks = decks.ToArray(),
                Tournament = tournament
            };
        }

        //private static Deck[] ParseDecks(HtmlDocument doc, Uri eventUri)
        //{

        //    List<Deck> result = new List<Deck>();
        //    var deckNodes = doc.DocumentNode.SelectNodes("//div[@class='deck-group']");
        //    if (deckNodes == null) return new Deck[0];

        //    foreach (var deckNode in deckNodes)
        //    {
        //        string anchor = deckNode.GetAttributeValue("id", "");
        //        string playerName = deckNode.SelectSingleNode("span[@class='deck-meta']/h4/text()")?.InnerText.Split("(").First().Trim();
        //        if (String.IsNullOrEmpty(playerName)) playerName = deckNode.SelectSingleNode("div[@class='title-deckicon']/span[@class='deck-meta']/h4/text()")?.InnerText.Split("(").First().Trim();

        //        string playerResult = deckNode.SelectSingleNode("span[@class='deck-meta']/h4/text()")?.InnerText.Split("(").Last().TrimEnd(')').Trim();
        //        if (String.IsNullOrEmpty(playerResult)) playerResult = deckNode.SelectSingleNode("div[@class='title-deckicon']/span[@class='deck-meta']/h4/text()")?.InnerText.Split("(").Last().TrimEnd(')').Trim();

        //        string deckDateText = deckNode.SelectSingleNode("span[@class='deck-meta']/h5/text()")?.InnerText.Split(" on ").Last().Trim();
        //        if (String.IsNullOrEmpty(deckDateText)) deckDateText = deckNode.SelectSingleNode("div[@class='title-deckicon']/span[@class='deck-meta']/h5/text()")?.InnerText.Split(" on ").Last().Trim();

        //        var decklistNode = deckNode.SelectSingleNode("div[@class='toggle-text toggle-subnav']/div[@class='deck-list-text']");
        //        var mainboardNode = decklistNode.SelectSingleNode("div[@class='sorted-by-overview-container sortedContainer']");
        //        var sideboardNode = decklistNode.SelectSingleNode("div[@class='sorted-by-sideboard-container  clearfix element']");

        //        DateTime? deckDate = null;
        //        if (!String.IsNullOrEmpty(deckDateText)) deckDate = DateTime.ParseExact(deckDateText, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();

        //        result.Add(new Deck()
        //        {
        //            Date = deckDate,
        //            Player = playerName,
        //            Result = playerResult,
        //            AnchorUri = new Uri($"{eventUri.ToString()}#{anchor}"),
        //            Mainboard = ParseCards(mainboardNode, false),
        //            Sideboard = ParseCards(sideboardNode, true)
        //        });
        //    }

        //    return result.ToArray();
        //}

        //private static DeckItem[] ParseCards(HtmlNode node, bool isSideboard)
        //{
        //    if (node == null) return new DeckItem[0];

        //    List<DeckItem> cards = new List<DeckItem>();
        //    var cardNodes = node.SelectNodes(isSideboard ? "span[@class='row']" : "div/span[@class='row']");
        //    if (cardNodes == null) return new DeckItem[0];

        //    foreach (var cardNode in cardNodes)
        //    {
        //        var cardCount = cardNode.SelectSingleNode("span[@class='card-count']").InnerText;
        //        var cardName = CardNameNormalizer.Normalize(HttpUtility.HtmlDecode(cardNode.SelectSingleNode("span[@class='card-name']").InnerText));

        //        cards.Add(new DeckItem()
        //        {
        //            Count = Int32.Parse(cardCount),
        //            CardName = cardName
        //        });
        //    }
        //    return (cards.ToArray());
        //}

        //private static Standing[] ParseStandings(HtmlDocument doc)
        //{
        //    var standingsRoot = doc.DocumentNode.SelectSingleNode("//table[@class='sticky-enabled']");
        //    if (standingsRoot == null) return null;

        //    List<Standing> result = new List<Standing>();

        //    var standingNodes = standingsRoot.SelectNodes("tbody/tr");
        //    foreach (var standingNode in standingNodes)
        //    {
        //        var rows = standingNode.SelectNodes("td");

        //        int rank = int.Parse(rows[0].InnerText);
        //        string player = rows[1].InnerText.Trim();
        //        int points = int.Parse(rows[2].InnerText);
        //        double omwp = double.Parse(rows[3].InnerText, CultureInfo.InvariantCulture);
        //        double gwp = double.Parse(rows[4].InnerText, CultureInfo.InvariantCulture);
        //        double ogwp = double.Parse(rows[5].InnerText, CultureInfo.InvariantCulture);

        //        result.Add(new Standing()
        //        {
        //            Rank = rank,
        //            Player = player,
        //            Points = points,
        //            OMWP = omwp,
        //            GWP = gwp,
        //            OGWP = ogwp
        //        });
        //    }

        //    return result.ToArray();
        //}

        //private static Bracket ParseBracket(HtmlDocument doc)
        //{
        //    var bracketRoot = doc.DocumentNode.SelectSingleNode("//div[@class='wrap-bracket-slider']");
        //    if (bracketRoot == null) return null;

        //    var bracketNodes = bracketRoot.SelectNodes("div/div[@class='finalists']");

        //    return new Bracket()
        //    {
        //        Quarterfinals = ParseBracketItem(bracketNodes.Skip(0).First()),
        //        Semifinals = ParseBracketItem(bracketNodes.Skip(1).First()),
        //        Finals = ParseBracketItem(bracketNodes.Skip(2).First()).First()
        //    };
        //}

        //private static BracketItem[] ParseBracketItem(HtmlNode node)
        //{
        //    var playerNodes = node.SelectNodes("div/div[@class='player']");

        //    List<string> players = new List<string>();
        //    foreach (var playerNode in playerNodes) players.Add(playerNode.InnerText);

        //    // Cleans up player names
        //    players = players
        //        .Select(p => p.Trim())
        //        .Select(p => Regex.Replace(p, @"^\(\d+\)\s*", ""))
        //        .ToList();

        //    List<BracketItem> result = new List<BracketItem>();
        //    for (var i = 0; i < players.Count; i = i + 2)
        //    {
        //        result.Add(new BracketItem()
        //        {
        //            Player1 = players[i].Split(",").First(),
        //            Result = players[i].Split(", ").Last(),
        //            Player2 = players.Count > 1 ? players[i + 1] : ""
        //        });
        //    }

        //    return result.ToArray();
        //}

        //private static DateTime ExtractDateFromUrl(Uri eventUri)
        //{
        //    string eventPath = eventUri.LocalPath;
        //    string[] eventPathSegments = eventPath.Split("-").Where(e => e.Length > 1).ToArray();
        //    string eventDate = String.Join("-", eventPathSegments.Skip(eventPathSegments.Length - 3).ToArray());

        //    if (DateTime.TryParse(eventDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime parsedDate))
        //    {
        //        return parsedDate.ToUniversalTime();
        //    }
        //    else
        //    {
        //        // This is only used to decide or not to bypass cache, so it's safe to return a fallback for today forcing the bypass
        //        return DateTime.UtcNow.Date;
        //    }
        //}
    }

}
