﻿using System;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Views.InputMethods;
using Avalonia.Android.Platform.SkiaPlatform;
using Avalonia.Input;
using Avalonia.Input.TextInput;

namespace Avalonia.Android
{
    internal interface IAndroidInputMethod
    {
        public View View { get; }

        public ITextInputMethodClient Client { get; }

        public bool IsActive { get; }

        public InputMethodManager IMM { get; }
    }

    enum CustomImeFlags
    { 
        ActionNone = 0x00000001,
       ActionGo = 0x00000002,
       ActionSearch = 0x00000003,
       ActionSend = 0x00000004,
       ActionNext = 0x00000005,
       ActionDone = 0x00000006,
       ActionPrevious = 0x00000007,
    }

    class AndroidInputMethod<TView> : ITextInputMethodImpl, IAndroidInputMethod
        where TView : View, IInitEditorInfo
    {
        private readonly TView _host;
        private readonly InputMethodManager _imm;
        private ITextInputMethodClient _client;
        private AvaloniaInputConnection _inputConnection;

        public AndroidInputMethod(TView host)
        {
            if (host.OnCheckIsTextEditor() == false)
                throw new InvalidOperationException("Host should return true from OnCheckIsTextEditor()");

            _host = host;
            _imm = host.Context.GetSystemService(Context.InputMethodService).JavaCast<InputMethodManager>();

            _host.Focusable = true;
            _host.FocusableInTouchMode = true;
            _host.ViewTreeObserver.AddOnGlobalLayoutListener(new SoftKeyboardListener(_host));
        }

        public View View => _host;

        public bool IsActive => Client != null;

        public ITextInputMethodClient Client => _client;

        public InputMethodManager IMM => _imm;

        public void Reset()
        {
            _imm.RestartInput(_host);
        }

        public void SetClient(ITextInputMethodClient client)
        {
            if(client is null)
            {
                _inputConnection?.SetComposingText("", 0);
            }

            if (_client != null)
            {
                _client.SurroundingTextChanged -= SurroundingTextChanged;
            }

            Reset();

            _client = client;

            if (IsActive)
            {
                _client.SurroundingTextChanged += SurroundingTextChanged;

                _host.RequestFocus();

                _imm.ShowSoftInput(_host, ShowFlags.Implicit);
            }
            else
            {
                _imm.HideSoftInputFromWindow(_host.WindowToken, HideSoftInputFlags.None);
            }
        }

        private void SurroundingTextChanged(object sender, EventArgs e)
        {
            if (IsActive)
            {
                var surroundingText = Client.SurroundingText;

                _inputConnection.SurroundingText = surroundingText;

                _imm.UpdateSelection(_host, surroundingText.AnchorOffset, surroundingText.CursorOffset, surroundingText.AnchorOffset, surroundingText.CursorOffset);
            }
        }

        public void SetCursorRect(Rect rect)
        {
            
        }

        public void SetOptions(TextInputOptions options)
        {
            _host.InitEditorInfo((topLevel, outAttrs) =>
            {
                _inputConnection = new AvaloniaInputConnection(topLevel, this);

                outAttrs.InputType = options.ContentType switch
                {
                    TextInputContentType.Email => global::Android.Text.InputTypes.TextVariationEmailAddress,
                    TextInputContentType.Number => global::Android.Text.InputTypes.ClassNumber,
                    TextInputContentType.Password => global::Android.Text.InputTypes.TextVariationPassword,
                    TextInputContentType.Digits => global::Android.Text.InputTypes.ClassPhone,
                    TextInputContentType.Url => global::Android.Text.InputTypes.TextVariationUri,
                    _ => global::Android.Text.InputTypes.ClassText
                };

                if (options.AutoCapitalization)
                {
                    outAttrs.InitialCapsMode = global::Android.Text.CapitalizationMode.Sentences;
                    outAttrs.InputType |= global::Android.Text.InputTypes.TextFlagCapSentences;
                }

                if (options.Multiline)
                    outAttrs.InputType |= global::Android.Text.InputTypes.TextFlagMultiLine;

                outAttrs.ImeOptions = options.ReturnKeyType switch
                {
                    TextInputReturnKeyType.Return => ImeFlags.NoEnterAction,
                    TextInputReturnKeyType.Go => (ImeFlags)CustomImeFlags.ActionGo,
                    TextInputReturnKeyType.Send => (ImeFlags)CustomImeFlags.ActionSend,
                    TextInputReturnKeyType.Search => (ImeFlags)CustomImeFlags.ActionSearch,
                    TextInputReturnKeyType.Next => (ImeFlags)CustomImeFlags.ActionNext,
                    TextInputReturnKeyType.Previous => (ImeFlags)CustomImeFlags.ActionPrevious,
                    _ => (ImeFlags)CustomImeFlags.ActionDone
                };

                outAttrs.ImeOptions |= ImeFlags.NoFullscreen | ImeFlags.NoExtractUi;

                return _inputConnection;
            });
        }

        private void RestoreSoftKeyboard(object sender, PointerReleasedEventArgs e)
        {
            _imm.ShowSoftInput(_host, ShowFlags.Implicit);
        }
    }
}
