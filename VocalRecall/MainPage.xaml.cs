using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using VocalRecall.SuggestopediaService;
using System.Windows.Media.Imaging;
using System.IO;
using System.Threading;

namespace VocalRecall
{
    public partial class MainPage : UserControl
    {
        private const int MAXIMUM_NUMBER_OF_RETURNED_ITEMS = 10;
        private const int NATIVE_CULTURE_ID = 2;
        private const int TARGET_CULTURE_ID = 4;
        private SuggestopediaService.SuggestopediaServiceClient spsc = new SuggestopediaService.SuggestopediaServiceClient();

        private Word[] loadedWords = null;
        private Word leftWord = null;
        private Word rightWord = null;

        private bool isInSession = false;

        public MainPage()
        {
            InitializeComponent();

            spsc.Get10WordsCompleted += new EventHandler<Get10WordsCompletedEventArgs>(spsc_Get10WordsCompleted);
            spsc.GetTranslationCompleted += new EventHandler<GetTranslationCompletedEventArgs>(spsc_GetTranslationCompleted);
            spsc.OpenAsync();

            sldrDifficulty_ValueChanged(this, null);
            btnLoad10Words_Click(this, null);
        }

        void spsc_GetTranslationCompleted(object sender, GetTranslationCompletedEventArgs e)
        {
            sldrWords.IsEnabled = true;

            if (e.Error != null) return;

            rightWord = e.Result;

            lblWordRight.Content = rightWord.Text;
            btnPlayRight.IsEnabled = rightWord.Pronunciation != null;

            if (isInSession)
            {
                btnSkip_Click(this, null);
            }
        }

        private void btnLoad10Words_Click(object sender, RoutedEventArgs e)
        {
            spsc.Get10WordsAsync(TARGET_CULTURE_ID, (int)sldrDifficulty.Value);
            btnLoad10Words.IsEnabled = false;
        }

        void spsc_Get10WordsCompleted(object sender, Get10WordsCompletedEventArgs e)
        {
            sldrWords.IsEnabled = true;
            btnLoad10Words.IsEnabled = true;

            if (e.Error == null)
            {
                loadedWords = e.Result.ToArray();
                sldrWords.Maximum = loadedWords.Length - 1;

                sldrWords.Value = 0;

                btnSkip.Focus();
            }
        }

        private void usrMainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            spsc.CloseAsync();
        }

        private void sldrWords_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            sldrWords.IsEnabled = false;
            
            lblWordRight.Content = "";
            btnPlayRight.IsEnabled = false;

            leftWord = loadedWords[(int)sldrWords.Value];

            lblWordLeft.Content = leftWord.Text;
            btnPlayLeft.IsEnabled = leftWord.Pronunciation != null;

            if (leftWord.Picture == null)
            {
                imgCenter.Source = null;
            }
            else
            {
                try
                {
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.SetSource(new MemoryStream(leftWord.Picture));
                    imgCenter.Source = bitmapImage;
                }
                catch
                {
                    imgCenter.Source = null;
                    spsc.DeletePictureAsync(leftWord.WordId);
                }
            }

            spsc.GetTranslationAsync(leftWord.WordId, NATIVE_CULTURE_ID);
        }

        private void btnPlayLeft_Click(object sender, RoutedEventArgs e)
        {
            if (leftWord.Pronunciation != null)
            {
                mediaElement.SetSource(new MemoryStream(leftWord.Pronunciation));
                mediaElement.Play();
            }
        }

        private void btnPlayRight_Click(object sender, RoutedEventArgs e)
        {
            if (rightWord.Pronunciation != null)
            {
                mediaElement.SetSource(new MemoryStream(rightWord.Pronunciation));                
                mediaElement.Play();
            }
        }

        private void sldrDifficulty_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lblDifficulty != null)
            {
                lblDifficulty.Content = (int)(sldrDifficulty.Value - MAXIMUM_NUMBER_OF_RETURNED_ITEMS) + " - " + (int)sldrDifficulty.Value;
            }
        }

        private void usrMainPage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.D1)
            {
                btnPlayLeft_Click(this, null);
            }

            if (e.Key == Key.D2)
            {
                btnPlayRight_Click(this, null);
            }

            if (e.Key == Key.Space)
            {
                btnSkip_Click(this, null);
            }
        }

        private void imgCenter_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            spsc.DeletePictureAsync(leftWord.WordId);
            imgCenter.Source = null;
        }

        private void btnPlaySession_Click(object sender, RoutedEventArgs e)
        {
            isInSession = true;
            btnLoad10Words_Click(this, null);
        }

        private void btnSkip_Click(object sender, RoutedEventArgs e)
        {
            if (sldrWords.Value < sldrWords.Maximum)
            {
                sldrWords.Value++;
            }
            else
            {
                sldrDifficulty.Value += MAXIMUM_NUMBER_OF_RETURNED_ITEMS;
                btnLoad10Words_Click(this, null);
            }
        }
    }
}
