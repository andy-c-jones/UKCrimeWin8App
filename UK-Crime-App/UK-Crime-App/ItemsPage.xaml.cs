using System;
using System.Collections.Generic;
using Bing.Maps;
using Newtonsoft.Json;
using RestSharp;
using UK_Crime_App.DTOs;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Location = Bing.Maps.Location;

namespace UK_Crime_App
{
    public sealed partial class ItemsPage
    {
        private Pushpin _lastPushpin = null;

        public ItemsPage()
        {
            InitializeComponent();
        }

        protected override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
        }

        private async void BtnSearchClick(object sender, RoutedEventArgs e)
        {
            const double longitude = -1.131592;
            const double latitude = 52.629729;

            var client = new RestClient("http://data.police.uk/api/");
            var request = new RestRequest(string.Format("crimes-street/all-crime?lat={0}&lng={1}", latitude, longitude), Method.GET);
            var jsonResponse = await client.ExecuteAsync(request);
            var test = jsonResponse.Content.Replace("null", "\"\"");
            var responseList = JsonConvert.DeserializeObject<List<Crime>>(test);

            listView1.ItemsSource = responseList;
            MyMap.Center = new Location(latitude, longitude);
            MyMap.ZoomLevel = 14.0;
        }

        private void ListViewSelectionChanged(object sender, SelectionChangedEventArgs selectionChangedEventArgs)
        {
            if (_lastPushpin != null)
            {
                MyMap.Children.Remove(_lastPushpin);
            }
            var crime = (Crime)selectionChangedEventArgs.AddedItems[0];

            var pushpin = new Pushpin();
            var location = new Location(double.Parse(crime.Location.Latitude), double.Parse(crime.Location.Longitude));
            MyMap.Center = location;
            MapLayer.SetPosition(pushpin, location);
            MyMap.Children.Add(pushpin);
            _lastPushpin = pushpin;
        }

    }
}
