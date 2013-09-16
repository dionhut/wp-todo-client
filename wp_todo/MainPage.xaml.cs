using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using wp_todo.Resources;
using Microsoft.Phone.Tasks;
using System.Net.Http;
using System.IO;

namespace wp_todo
{
    public class TodoItem
    {
        public int Id { get; set; }

        [JsonProperty(PropertyName = "text")]
        public string Text { get; set; }

        [JsonProperty(PropertyName = "complete")]
        public bool Complete { get; set; }

        [JsonProperty(PropertyName = "channel")]
        public string Channel { get; set; }

        [JsonProperty(PropertyName = "photoName")]
        public string PhotoName { get; set; }

        [JsonProperty(PropertyName = "photoSAS")]
        public string PhotoSAS { get; set; }
    }

    public partial class MainPage : PhoneApplicationPage
    {
        CameraCaptureTask cameraCaptureTask;
        PhotoResult lastTakenPhoto = null;

        private MobileServiceUser user;
        private async System.Threading.Tasks.Task Authenticate()
        {
            while (user == null)
            {
                string message;
                try
                {
                    user = await App.MobileService
                        .LoginAsync(MobileServiceAuthenticationProvider.MicrosoftAccount);
                    message =
                        string.Format("You are now logged in - {0}", user.UserId);
                }
                catch (InvalidOperationException)
                {
                    message = "You must log in. Login Required";
                }

                MessageBox.Show(message);
            }
        }
        
        // MobileServiceCollectionView implements ICollectionView (useful for databinding to lists) and 
        // is integrated with your Mobile Service to make it easy to bind your data to the ListView
        private MobileServiceCollection<TodoItem, TodoItem> items;

        private IMobileServiceTable<TodoItem> todoTable = App.MobileService.GetTable<TodoItem>();

        // Constructor
        public MainPage()
        {
            InitializeComponent();
            this.Loaded += MainPage_Loaded;

            cameraCaptureTask = new CameraCaptureTask();
            cameraCaptureTask.Completed += new EventHandler<PhotoResult>(cameraCaptureTask_Completed);
        }

        private async void InsertTodoItem(TodoItem todoItem)
        {
            // This code inserts a new TodoItem into the database. When the operation completes
            // and Mobile Services has assigned an Id, the item is added to the CollectionView
            await todoTable.InsertAsync(todoItem);
            items.Add(todoItem);

            // Upload image (if one was taken)
            if (this.lastTakenPhoto != null)
            {
                //Upload image with HttpClient to the blob service using the generated item.SAS
                using (var client = new HttpClient())
                {
                    var content = new StreamContent(lastTakenPhoto.ChosenPhoto);
                    content.Headers.Add("Content-Type", "image/jpeg");
                    content.Headers.Add("x-ms-blob-type", "BlockBlob");

                    var uploadResponse = await client.PutAsync(new Uri(todoItem.PhotoSAS), content);
                    this.lastTakenPhoto = null;
                }
            }
        }

        private async void RefreshTodoItems()
        {
            // This code refreshes the entries in the list view be querying the TodoItems table.
            // The query excludes completed TodoItems
            try
            {
                items = await todoTable
                    .Where(todoItem => todoItem.Complete == false)
                    .ToCollectionAsync();
            }
            catch (MobileServiceInvalidOperationException e)
            {
                MessageBox.Show(e.Message, "Error loading items", MessageBoxButton.OK);
            }

            ListItems.ItemsSource = items;
        }

        private async void UpdateCheckedTodoItem(TodoItem item)
        {
            // This code takes a freshly completed TodoItem and updates the database. When the MobileService 
            // responds, the item is removed from the list 
            await todoTable.UpdateAsync(item);
            items.Remove(item);
        }

        private void ButtonRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshTodoItems();
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {

            var todoItem = new TodoItem
            {
                Text = TodoInput.Text,
                Channel = App.CurrentChannel.ChannelUri.ToString()
            };
            if (lastTakenPhoto != null)
            {
                todoItem.PhotoName = System.IO.Path.GetFileName(lastTakenPhoto.OriginalFileName);
            }
            InsertTodoItem(todoItem);
            TodoInput.Text = "";
        }

        private void CheckBoxComplete_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            TodoItem item = cb.DataContext as TodoItem;
            item.Complete = true;
            item.Channel = App.CurrentChannel.ChannelUri.ToString();
            UpdateCheckedTodoItem(item);
        }

        async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            await Authenticate();
            RefreshTodoItems();
        }

        private void ButtonTakePhoto_Click(object sender, RoutedEventArgs e)
        {
            lastTakenPhoto = null;
            cameraCaptureTask.Show();
        }

        private void cameraCaptureTask_Completed(object sender, PhotoResult e)
        {
            if (e.TaskResult == TaskResult.OK)
            {
                this.lastTakenPhoto = e;
            }
        }
    }
}