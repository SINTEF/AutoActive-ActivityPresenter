﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ImportParametersPage : ContentPage
    {
        public Dictionary<string, object> Parameters
        {
            get
            {
                var parameters = new Dictionary<string, object>();
                var childIndex = 0;

                foreach (var _ in _parameters)
                {
                    var element = (ParameterGrid.Children[childIndex++] as StackLayout)?.Children[0];
                    var name = (element as Label)?.Text;
                    if (name == null)
                    {
                        Debug.WriteLine("Could not find name");
                        continue;
                    }
                    var value = ParameterGrid.Children[childIndex++];
                    parameters[name] = GetValueFromView(value);
                }

                return parameters;
            }
        }

        private readonly Dictionary<string, (object, string)> _parameters;

        public ImportParametersPage()
        {
            InitializeComponent();
            _parameters = new Dictionary<string, (object, string)>();
        }

        public ImportParametersPage(string filename, Dictionary<string, (object, string)> parameters)
        {
            InitializeComponent();
            _parameters = parameters;
            TitleLabel.Text = $"Import properties for {filename}";

            PopulateParameters();
        }

        private void PopulateParameters()
        {
            ParameterGrid.Children.Clear();

            var rowPosition = 0;

            foreach (var kv in _parameters)
            {
                ParameterGrid.RowDefinitions.Add(new RowDefinition());

                var stack = new StackLayout();

                var propertyName = new Label
                {
                    Text = kv.Key,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Start
                };
                stack.Children.Add(propertyName);

                var descriptionName = new Label
                {
                    Text = kv.Value.Item2,
                    HorizontalTextAlignment = TextAlignment.Start
                };
                stack.Children.Add(descriptionName);

                Grid.SetRow(stack, rowPosition);
                Grid.SetColumn(stack, 1);
                ParameterGrid.Children.Add(stack);

                var obj = kv.Value.Item1;
                var value = GetViewFromValue(obj);
                if (value == null)
                {
                    Debug.WriteLine($"Unknown object value type: {obj}");
                    continue;
                }

                Grid.SetRow(value, rowPosition);
                Grid.SetColumn(value, 2);
                ParameterGrid.Children.Add(value);

                rowPosition++;
            }
        }

        private static View GetViewFromValue(object obj)
        {
            switch (obj)
            {
                case string objVal:
                    return new Entry {Text = objVal, WidthRequest = 250};
                case bool objVal:
                {
                    var btn = new Button();
                    btn.Text = objVal ? btn.Text = "Yes" : btn.Text = "No";

                    btn.Clicked += (sender, args) => btn.Text = btn.Text == "Yes" ? btn.Text = "No" : btn.Text = "Yes";
                    return btn;
                }
                default:
                    return null;
            }

        }

        private object GetValueFromView(View value)
        {
            switch (value)
            {
                case Entry objVal:
                    return objVal.Text;
                case Button objVal:
                {
                    return objVal.Text == "Yes";
                }
                default:
                    return null;
            }
        }

        private async void Ok_OnClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}