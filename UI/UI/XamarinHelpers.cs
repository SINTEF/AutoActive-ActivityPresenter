using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SINTEF.AutoActive.UI.Interfaces;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI
{
    public static class XamarinHelpers
    {
        public static Page GetCurrentPage(INavigation navigation)
        {
            return navigation.NavigationStack.LastOrDefault();
        }

        public static Page GetCurrentPage(View view)
        {
            return GetCurrentPage(view.Navigation);;
        }

        public static Page GetCurrentPage()
        {
            return GetCurrentPage(Application.Current.MainPage.Navigation);
        }

        public static void EnsureMainThread(Action action)
        {
            if (Device.IsInvokeRequired)
                Device.BeginInvokeOnMainThread(action);
            else
                action();
        }

        public static IFigureContainer GetFigureContainerFromParents(Element element)
        {
            if (element == null) return null;
            var container = GetTypedElementFromParents<IFigureContainer>(element);
            return container ?? throw new ArgumentException("Layout not recognized");
        }

        public static T GetTypedElementFromParents<T>(Element element)
        {
            while (element != null)
            {
                if (element is T container)
                {
                    return container;
                }
                element = element.Parent;
            }

            return default(T);
        }

        public static List<T> GetAllChildElements<T>(Element element)
        {
            var list = new List<T>();
            var elements = new Queue<Element>();
            elements.Enqueue(element);

            while (elements.Any())
            {
                var el = elements.Dequeue();

                if (el is Layout layout)
                {
                    foreach (var child in layout.Children)
                    {
                        elements.Enqueue(child);
                    }
                }

                if (el is T item)
                    list.Add(item);
            }

            return list;
        }

        public static T GetFirstChildElement<T>(Element element)
        {
            var elements = new Queue<Element>();
            elements.Enqueue(element);

            while (elements.Any())
            {
                var el = elements.Dequeue();

                if (el is Layout layout)
                {
                    foreach (var child in layout.Children)
                    {
                        elements.Enqueue(child);
                    }
                }

                if (el is T item)
                    return item;
            }
            return default(T);
        }

        public static async Task ShowOkMessage(string title, string message, Page page = null)
        {
            if (page == null)
            {
                page = Application.Current.MainPage;
            }
            await page.DisplayAlert(title, message, "OK");
        }
    }
}
