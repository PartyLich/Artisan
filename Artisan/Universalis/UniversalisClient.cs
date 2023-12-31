using Dalamud.Logging;
using ECommons;
using ECommons.DalamudServices;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Artisan.Universalis
{
    internal class UniversalisClient
    {
        private const string Endpoint = "https://universalis.app/api/v2/";
        private readonly HttpClient httpClient;

        public UniversalisClient()
        {
            this.httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(10000),
            };
        }

        public List<MarketboardData?> GetMarketBoard(string region, IEnumerable<ulong> itemId)
        {
            var marketBoardFromAPI = this.GetMarketBoardData(region, itemId);
            return marketBoardFromAPI;
        }

        public MarketboardData? GetRegionData(ulong itemId)
        {
            var world = Svc.ClientState.LocalPlayer?.CurrentWorld.Id;
            if (world == null)
                return null;

            var region = Regions.GetRegionByWorld(world.Value);
            if (region == null)
                return null;


            return GetMarketBoard(region, [itemId])[0];
        }

        public List<MarketboardData?>? GetRegionData(IEnumerable<ulong> itemIds)
        {
            var world = Svc.ClientState.LocalPlayer?.CurrentWorld.Id;
            if (world == null)
                return null;

            var region = Regions.GetRegionByWorld(world.Value);
            if (region == null)
                return null;

            return GetMarketBoard(region, itemIds);
        }

        public MarketboardData? GetDataCenterData(ulong itemId)
        {
            var world = Svc.ClientState.LocalPlayer?.CurrentWorld.Id;
            if (world == null)
                return null;

            var datacenter = DataCenters.GetDataCenterNameByWorld(world.Value);
            if (datacenter == null)
                return null;

            return GetMarketBoard(datacenter, [itemId])[0];
        }

        public List<MarketboardData?>? GetDataCenterData(IEnumerable<ulong> itemIds)
        {
            var world = Svc.ClientState.LocalPlayer?.CurrentWorld.Id;
            if (world == null)
                return null;

            var datacenter = DataCenters.GetDataCenterNameByWorld(world.Value);
            if (datacenter == null)
                return null;

            return GetMarketBoard(datacenter, itemIds);
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }

        private static MarketboardData? ParseMarketData(dynamic json)
        {
            var marketBoardData = new MarketboardData
            {
                LastCheckTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                LastUploadTime = json.lastUploadTime?.Value,
                AveragePriceNQ = json.averagePriceNQ?.Value,
                AveragePriceHQ = json.averagePriceHQ?.Value,
                CurrentAveragePriceNQ = json.currentAveragePriceNQ?.Value,
                CurrentAveragePriceHQ = json.currentAveragePriceHQ?.Value,
                MinimumPriceNQ = json.minPriceNQ?.Value,
                MinimumPriceHQ = json.minPriceHQ?.Value,
                MaximumPriceNQ = json.maxPriceNQ?.Value,
                MaximumPriceHQ = json.maxPriceHQ?.Value,
                TotalNumberOfListings = json.listingsCount?.Value,
                TotalQuantityOfUnits = json.unitsForSale?.Value
            };

            if (json.listings.Count > 0)
            {
                foreach (var item in json.listings)
                {
                    Listing listing = new()
                    {
                        World = item.worldName.Value,
                        Quantity = item.quantity.Value,
                        TotalPrice = item.total.Value,
                        UnitPrice = item.pricePerUnit.Value
                    };

                    marketBoardData.AllListings.Add(listing);
                }

                marketBoardData.CurrentMinimumPrice = marketBoardData.AllListings.First().TotalPrice;
                marketBoardData.LowestWorld = marketBoardData.AllListings.First().World;
                marketBoardData.ListingQuantity = marketBoardData.AllListings.First().Quantity;
            }

            return marketBoardData;
        }

        private List<MarketboardData?>? GetMarketBoardData(string region, IEnumerable<ulong> itemIds)
        {
            if (!itemIds.Any()) return null;

            HttpResponseMessage result;
            try
            {
                result = this.GetMarketBoardDataAsync(region, itemIds).Result;
            }
            catch (Exception ex)
            {
                ex.Log();
                return null;
            }


            if (result.StatusCode != HttpStatusCode.OK)
            {
                PluginLog.LogError(
                    "Failed to retrieve data from Universalis for itemId {0} / worldId {1} with HttpStatusCode {2}.",
                    String.Join(",", itemIds),
                    region,
                    result.StatusCode);
                return null;
            }

            var json = JsonConvert.DeserializeObject<dynamic>(result.Content.ReadAsStringAsync().Result);
            if (json == null)
            {
                PluginLog.LogError(
                    "Failed to deserialize Universalis response for itemId {0} / worldId {1}.",
                     String.Join(",", itemIds),
                    region);
                return null;
            }

            try
            {
                return (itemIds.Count() == 1)
                    ? [ParseMarketData(json)]
                    : itemIds.Select<ulong, MarketboardData?>(id => ParseMarketData(json.items[$"{id}"])).ToList();
            }
            catch (Exception ex)
            {
                PluginLog.LogError(
                    ex,
                    "Failed to parse marketBoard data for itemId {0} / worldId {1}.",
                    String.Join(",", itemIds),
                    region);
                return null;
            }
        }

        private async Task<HttpResponseMessage> GetMarketBoardDataAsync(string? worldId, IEnumerable<ulong> itemIds)
        {
            var request = Endpoint + worldId + "/" + String.Join(",", itemIds);
            PluginLog.LogDebug($"universalisRequest={request}");
            return await this.httpClient.GetAsync(new Uri(request));
        }
    }
}
