using System;
using System.Collections.Generic;
using RestSharp;
using UK_Crime_App.DataModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace UK_Crime_App
{
    public sealed partial class ItemsPage
    {
        public ItemsPage()
        {
            this.InitializeComponent();
        }

        protected override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
            //Load Map


            // TODO: Create an appropriate data model for your problem domain to replace the sample data
            //var sampleDataGroups = SampleDataSource.GetGroups((String)navigationParameter);
            //this.DefaultViewModel["Items"] = sampleDataGroups;
        }

        void ItemViewItemClick(object sender, ItemClickEventArgs e)
        {
            var groupId = ((SampleDataGroup)e.ClickedItem).UniqueId;
            this.Frame.Navigate(typeof(SplitPage), groupId);
        }

        private async void BtnSearchClick(object sender, RoutedEventArgs e)
        {
            const double longitude = -1.131592;
            const double latitude = 52.629729;

            var client = new RestClient("http://data.police.uk/api/");
            var request = new RestRequest(string.Format("crimes-street/all-crime?lat={0}&lng={1}", latitude, longitude), Method.GET);
            var response = await client.ExecuteAsync(request);
        }

    }
}
