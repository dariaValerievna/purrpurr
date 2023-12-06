using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace purrpurrPlugin
{
    /// <summary>
    /// Логика взаимодействия для UserWindowControl.xaml
    /// </summary>
    public partial class UserWindowControl : Window
    {
        int RoomCount { get; set; }
        bool IsSeparateBathrooms { get; set; } = false;
        bool IsWardrobe { get; set; } = false;
        bool IsCombinedKitchen { get; set; } = false;
        bool IsLoggia { get; set; } = false;
        int HouseClass { get; set; }
        PluginEngine Engine;
        private static Dictionary<string, int> Mapping { get; } = new Dictionary<string, int>()
        {
            { "Эконом", 0 },
            { "Комфорт", 1},
            { "Комфорт+", 2},
            { "Бизнес", 3}
        };
        public UserWindowControl()
        {
            InitializeComponent();
        }

        private void NumLivingRooms(object sender, TextChangedEventArgs e) // Количество жилых комнат
        {
            int numericValue;
            if (!int.TryParse(LivingRooms.Text, out numericValue) && LivingRooms.Text != "")
                LRExeption.Text = "Значение не является числом!";
            else
                LRExeption.Text = "";
        }

        private void NumBathrooms(object sender, TextChangedEventArgs e) // Количество санузлов
        {
            int numericValue;
            if (!int.TryParse(Bathrooms.Text, out numericValue) && Bathrooms.Text != "")
                NBExeption.Text = "Значение не является числом!";
            else
                NBExeption.Text = "";
        }

        private void SeparateBathrooms(object sender, RoutedEventArgs e) // Раздельные санузлы 
        {
            IsSeparateBathrooms = !IsSeparateBathrooms;
        }

        private void Wardrobe(object sender, RoutedEventArgs e) // Наличие гардероба
        {
            IsWardrobe = !IsWardrobe;
        }

        private void CombinedKitchen(object sender, RoutedEventArgs e) // Совмещенная кухня
        {
            IsCombinedKitchen = !IsCombinedKitchen;
        }

        private void Loggia(object sender, RoutedEventArgs e) // Наличие лоджии
        {
            IsLoggia = !IsLoggia;
        }

        private void HousingClass(object sender, RoutedEventArgs e) // Класс жилья
        {
            RadioButton butt = sender as RadioButton;
            HouseClass = Mapping[butt.Content.ToString()];
        }

        private void ButtonClickSave(object sender, RoutedEventArgs e) // Кнопка "Сохранить"
        {
            Engine = new PluginEngine(RoomCount, IsSeparateBathrooms, IsWardrobe, IsCombinedKitchen, IsLoggia, HouseClass);
        }

        private void ButtonClickGeneration(object sender, RoutedEventArgs e) // Кнопка "Генерация"
        {
            Engine.Run();
        }
    }
}
