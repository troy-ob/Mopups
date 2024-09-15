﻿using Android.Views;
using Android.Widget;
using AsyncAwaitBestPractices;
using Mopups.Interfaces;
using Mopups.Pages;
using Mopups.Services;

namespace Mopups.Droid.Implementation;

public class AndroidMopups : IPopupPlatform
{
    private static FrameLayout? DecoreView => Platform.CurrentActivity?.Window?.DecorView as FrameLayout;

    public static bool SendBackPressed(Action? backPressedHandler = null)
    {
        var popupNavigationInstance = MopupService.Instance;

        if(popupNavigationInstance.PopupStack.Count > 0)
        {
            var lastPage = popupNavigationInstance.PopupStack[popupNavigationInstance.PopupStack.Count - 1];

            var isPreventClose = lastPage.SendBackButtonPressed();

            if(!isPreventClose)
            {
                popupNavigationInstance.PopAsync().SafeFireAndForget();
            }

            return true;
        }

        backPressedHandler?.Invoke();

        return false;
    }

    public Task AddAsync(PopupPage page)
    {
        HandleAccessibility(true, page.DisableAndroidAccessibilityHandling, page);

        page.Parent = MauiApplication.Current.Application.Windows[0].Content as Element;
        page.Parent ??= MauiApplication.Current.Application.Windows[0].Content as Element;

        var handler = page.Handler ??= new PopupPageHandler(page.Parent.Handler.MauiContext);

        var androidNativeView = handler.PlatformView as Android.Views.View;
        var decoreView = Platform.CurrentActivity?.Window?.DecorView as FrameLayout;

        decoreView?.AddView(androidNativeView);

        return PostAsync(androidNativeView);
    }

    public Task RemoveAsync(PopupPage page)
    {
        var renderer = IPopupPlatform.GetOrCreateHandler<PopupPageHandler>(page);

        if(renderer != null)
        {
            HandleAccessibility(false, page.DisableAndroidAccessibilityHandling, page);

            DecoreView?.RemoveView(renderer.PlatformView as Android.Views.View);
            renderer.DisconnectHandler(); //?? no clue if works
            page.Parent = null;

            return PostAsync(DecoreView);
        }

        return Task.CompletedTask;
    }

    //! important keeps reference to pages that accessibility has applied to. This is so accessibility can be removed properly when popup is removed. #https://github.com/LuckyDucko/Mopups/issues/93
    readonly Dictionary<Type, List<Android.Views.View>> accessibilityStates = new();
    void HandleAccessibility(bool showPopup, bool disableAccessibilityHandling, PopupPage popup)
    {
        if(disableAccessibilityHandling)
        {
            return;
        }

        if(showPopup)
        {
            Page? mainPage = popup.Parent as Page ?? Application.Current?.MainPage;

            if(mainPage is null)
            {
                return;
            }

            List<Android.Views.View> views = [];

            var mainPageAndroidView = mainPage.Handler?.PlatformView as Android.Views.View;
            if(mainPageAndroidView is not null && mainPageAndroidView.ImportantForAccessibility != ImportantForAccessibility.NoHideDescendants)
            {
                views.Add(mainPageAndroidView);
            }

            int navCount = mainPage.Navigation.NavigationStack.Count;
            if(navCount > 0)
            {
                var androidView = mainPage.Navigation.NavigationStack[navCount - 1]?.Handler?.PlatformView as Android.Views.View;

                if(androidView is not null && androidView.ImportantForAccessibility != ImportantForAccessibility.NoHideDescendants)
                {
                    views.Add(androidView);
                }
            }

            int modalCount = mainPage.Navigation.ModalStack.Count;
            if(modalCount > 0)
            {
                var androidView = mainPage.Navigation.ModalStack[modalCount - 1]?.Handler?.PlatformView as Android.Views.View;
                if(androidView is not null && androidView.ImportantForAccessibility != ImportantForAccessibility.NoHideDescendants)
                {
                    views.Add(androidView);
                }
            }

            var popupCount = MopupService.Instance.PopupStack.Count;
            if(popupCount > 1)
            {
                var androidView = MopupService.Instance.PopupStack[popupCount - 2]?.Handler?.PlatformView as Android.Views.View;
                if(androidView is not null && androidView.ImportantForAccessibility != ImportantForAccessibility.NoHideDescendants)
                {
                    views.Add(androidView);
                }
            }
            
            accessibilityStates.Add(popup.GetType(), views);
        }

        if(accessibilityStates.ContainsKey(popup.GetType()))
        {
            foreach(var view in accessibilityStates[popup.GetType()])
            {
                ProcessView(showPopup, view);
            }

            if(!showPopup)
            {
                accessibilityStates.Remove(popup.GetType());
            }
        }

        static void ProcessView(bool showPopup, Android.Views.View? view)
        {
            if(view is null)
            {
                return;
            }

            // Screen reader
            view.ImportantForAccessibility = showPopup ? ImportantForAccessibility.NoHideDescendants : ImportantForAccessibility.Auto;

            // Keyboard navigation
            ((ViewGroup)view).DescendantFocusability = showPopup ? DescendantFocusability.BlockDescendants : DescendantFocusability.AfterDescendants;
            view.ClearFocus();
        }
    }

    static Task<bool> PostAsync(Android.Views.View? nativeView)
    {
        if(nativeView == null)
        {
            return Task.FromResult(true);
        }

        var tcs = new TaskCompletionSource<bool>();

        nativeView.Post(() => tcs.SetResult(true));

        return tcs.Task;
    }
}
