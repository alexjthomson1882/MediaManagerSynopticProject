using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace MediaManager {

    public sealed class InputBox {

        #region delegate

        public delegate void OnInputBoxSubmittedDelegate(in string value, in bool cancelled, in object data);

        #endregion

        #region InputBoxEntry

        private sealed class InputBoxEntry {

            #region variable

            public readonly OnInputBoxSubmittedDelegate callback;
            public readonly string hintText;
            public readonly string defaultValue;
            public readonly object data;

            #endregion

            #region constructor

            public InputBoxEntry(in OnInputBoxSubmittedDelegate callback, in string hintText, in string defaultValue, in object data) {

                this.callback = callback;
                this.hintText = hintText;
                this.defaultValue = defaultValue;
                this.data = data;

            }

            #endregion

        }

        #endregion

        #region variable

        /// <summary>
        /// Grid that contains the input box UI.
        /// </summary>
        private readonly Grid grid;

        private readonly TextBlock hint;

        private readonly TextBox input;

        /// <summary>
        /// Queues all questions that need to be asked.
        /// </summary>
        private readonly Queue<InputBoxEntry> questionQueue;

        /// <summary>
        /// Tracks if a question is being asked or not.
        /// </summary>
        private InputBoxEntry currentQueston;

        #endregion

        #region constructor

        public InputBox(in Grid grid) {

            this.grid = grid ?? throw new ArgumentNullException("grid");
            questionQueue = new Queue<InputBoxEntry>();
            currentQueston = null;

            hint = (TextBlock)grid.FindName("InputBox_Hint");
            input = (TextBox)grid.FindName("InputBox_Input");
            input.TextChanged += OnTextChanged;
            ((Button)grid.FindName("InputBox_Submit")).Click += OnSubmit;

        }

        #endregion

        #region logic

        #region OnTextChanged

        private void OnTextChanged(object sender, TextChangedEventArgs e) => hint.Visibility = string.IsNullOrEmpty(input.Text) ? Visibility.Visible : Visibility.Collapsed;

        #endregion

        #region OnSubmit

        private void OnSubmit(object sender, RoutedEventArgs e) {

            if (currentQueston == null) {

                TryAskQuestion();
                return;

            }

            try {

                string value = input.Text;
                currentQueston.callback(
                    value,
                    false,
                    currentQueston.data
                ); // call callback

            } finally {

                currentQueston = null; // the current question has been processed
                TryAskQuestion(); // try ask another question

            }

        }

        #endregion

        #region AskQuestion

        public void AskQuestion(in OnInputBoxSubmittedDelegate callback, in string hintText, in string defaultValue = null, in object data = null, in bool cancelExistingQuestions = false) {

            if (callback == null) throw new ArgumentNullException("callback");

            if (cancelExistingQuestions) {

                while (questionQueue.TryDequeue(out InputBoxEntry e)) {

                    try {

                        e.callback(e.defaultValue, true, e.data);

                    } catch (Exception exception) {

                        Trace.WriteLine(exception.Message);

                    }

                }

            }

            questionQueue.Enqueue(
                new InputBoxEntry(
                    callback,
                    hintText,
                    defaultValue,
                    data
                )
            );

            TryAskQuestion();

        }

        #endregion

        #region TryAskQuestion

        private void TryAskQuestion() {

            if (currentQueston != null) return; // ignore, already asking a question

            if (!questionQueue.TryDequeue(out currentQueston)) { // try get the next question

                grid.Visibility = Visibility.Collapsed;
                return;

            }

            hint.Text = currentQueston.hintText ?? string.Empty;
            input.Text = currentQueston.defaultValue ?? string.Empty;
            grid.Visibility = Visibility.Visible;
            input.Focus();

        }

        #endregion

        #endregion

    }

}
