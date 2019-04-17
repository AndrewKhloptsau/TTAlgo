﻿using System;
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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TickTrader.BotTerminal
{
    /// <summary>
    /// Interaction logic for BAAccountDialogView.xaml
    /// </summary>
    public partial class BAAccountDialogView : Window
    {
        public BAAccountDialogView()
        {
            InitializeComponent();
            Loaded += BAAccountDialogView_Loaded;
        }

        private void BAAccountDialogView_Loaded(object sender, RoutedEventArgs e)
        {
            var pwdContainer = DataContext as IPasswordContainer;

            if (pwdContainer != null)
            {
                PasswordInput.Password = pwdContainer.Password;
                PasswordInput.PasswordChanged += (s, a) => pwdContainer.Password = PasswordInput.Password;
                pwdContainer.PropertyChanged += (s, a) =>
                {
                    if (a.PropertyName == nameof(IPasswordContainer.Password) && pwdContainer.Password != PasswordInput.Password)
                        PasswordInput.Password = pwdContainer.Password;
                };
            }
        }
    }
}