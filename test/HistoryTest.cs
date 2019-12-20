﻿using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        private void SetHistory(params string[] historyItems)
        {
            PSConsoleReadLine.ClearHistory();
            foreach (var item in historyItems)
            {
                PSConsoleReadLine.AddToHistory(item);
            }
        }

        [SkippableFact]
        public void History()
        {
            TestSetup(KeyMode.Cmd);

            // No history
            SetHistory();
            Test("", Keys(_.UpArrow, _.DownArrow));

            SetHistory("dir c*", "ps p*");

            Test("dir c*", Keys(_.UpArrow, _.UpArrow));
            Test("dir c*", Keys(_.UpArrow, _.UpArrow, _.DownArrow));
        }

        [SkippableFact]
        public void SensitiveHistoryDefaultBehavior()
        {
            TestSetup(KeyMode.Cmd);

            // No history
            SetHistory();
            Test("", Keys(_.UpArrow, _.DownArrow));

            var options = PSConsoleReadLine.GetOptions();
            var oldHistoryFilePath = options.HistorySavePath;
            var oldHistorySaveStyle = options.HistorySaveStyle;

            // AddToHistoryHandler should be set to the default handler.
            Assert.Same(PSConsoleReadLineOptions.DefaultAddToHistoryHandler, options.AddToHistoryHandler);

            var newHistoryFilePath = Path.GetTempFileName();
            var newHistorySaveStyle = HistorySaveStyle.SaveIncrementally;

            string[] expectedHistoryItems = new[] {
                "gcm c*",
                "ConvertTo-SecureString -AsPlainText -String abc -Force",
                "dir p*",
                "Publish-Module -NuGetApiKey abc",
                "ps c*",
                "mycommand -password abc",
                "echo foo",
                "cmd1 /token abc",
                "echo bar",
                "cmd2 --key abc",
                "echo zoo",
                "pki secret",
                "gcm p*"
            };

            string[] expectedSavedItems = new[] {
                "gcm c*",
                "dir p*",
                "ps c*",
                "echo foo",
                "echo bar",
                "echo zoo",
                "gcm p*"
            };

            try
            {
                options.HistorySavePath = newHistoryFilePath;
                options.HistorySaveStyle = newHistorySaveStyle;
                SetHistory(expectedHistoryItems);

                // Sensitive input history should be kept in the internal history queue.
                var historyItems = PSConsoleReadLine.GetHistoryItems();
                Assert.Equal(expectedHistoryItems.Length, historyItems.Length);
                for (int i = 0; i < expectedHistoryItems.Length; i++)
                {
                    Assert.Equal(expectedHistoryItems[i], historyItems[i].CommandLine);
                }

                // Sensitive input history should NOT be saved to the history file.
                string[] text = File.ReadAllLines(newHistoryFilePath);
                Assert.Equal(expectedSavedItems.Length, text.Length);
                for (int i = 0; i < text.Length; i++)
                {
                    Assert.Equal(expectedSavedItems[i], text[i]);
                }
            }
            finally
            {
                options.HistorySavePath = oldHistoryFilePath;
                options.HistorySaveStyle = oldHistorySaveStyle;
                File.Delete(newHistoryFilePath);
            }
        }

        [SkippableFact]
        public void SensitiveHistoryOptionalBehavior()
        {
            TestSetup(KeyMode.Cmd);

            // No history
            SetHistory();
            Test("", Keys(_.UpArrow, _.DownArrow));

            var options = PSConsoleReadLine.GetOptions();
            var oldHistoryFilePath = options.HistorySavePath;
            var oldHistorySaveStyle = options.HistorySaveStyle;

            // AddToHistoryHandler should be set to the default handler.
            Assert.Same(PSConsoleReadLineOptions.DefaultAddToHistoryHandler, options.AddToHistoryHandler);

            var newHistoryFilePath = Path.GetTempFileName();
            var newHistorySaveStyle = HistorySaveStyle.SaveIncrementally;
            Func<string, object> newAddToHistoryHandler_ReturnBool = s => s.Contains("gal");
            Func<string, object> newAddToHistoryHandler_ReturnEnum =
                s => s.Contains("gal")
                    ? AddToHistoryOption.MemoryOnly
                    : s.Contains("gmo")
                        ? AddToHistoryOption.SkipAdding
                        : AddToHistoryOption.MemoryAndFile;
            Func<string, object> newAddToHistoryHandler_ReturnOther = s => "string value";

            string[] commandInputs = new[] {
                "gmo p*",
                "gcm c*",
                "gal dir",
                "ConvertTo-SecureString -AsPlainText -String abc -Force"
            };

            string[] expectedQueuedItems = new[] {
                "gcm c*",
                "gal dir",
                "ConvertTo-SecureString -AsPlainText -String abc -Force"
            };

            string[] expectedSavedItems = new[] {
                "gcm c*",
                "ConvertTo-SecureString -AsPlainText -String abc -Force"
            };

            try
            {
                options.HistorySavePath = newHistoryFilePath;
                options.HistorySaveStyle = newHistorySaveStyle;

                //
                // Set null to the handler means we don't do the check.
                //
                options.AddToHistoryHandler = null;
                SetHistory(commandInputs);

                // All commands should be kept in the internal history queue.
                var historyItems = PSConsoleReadLine.GetHistoryItems();
                Assert.Equal(commandInputs.Length, historyItems.Length);
                for (int i = 0; i < commandInputs.Length; i++)
                {
                    Assert.Equal(commandInputs[i], historyItems[i].CommandLine);
                }

                // All commands are saved to the history file when 'ScrubSensitiveHistory' is set to 'false'.
                string[] text = File.ReadAllLines(newHistoryFilePath);
                Assert.Equal(commandInputs.Length, text.Length);
                for (int i = 0; i < text.Length; i++)
                {
                    Assert.Equal(commandInputs[i], text[i]);
                }

                //
                // Use a handler that return boolean value.
                //   true: Add to memory and file
                //   false: Skip adding to history
                //
                options.AddToHistoryHandler = newAddToHistoryHandler_ReturnBool;
                // Clear the history file.
                File.WriteAllText(newHistoryFilePath, string.Empty);
                SetHistory(commandInputs);

                historyItems = PSConsoleReadLine.GetHistoryItems();
                Assert.Single(historyItems);
                Assert.Equal("gal dir", historyItems[0].CommandLine);

                text = File.ReadAllLines(newHistoryFilePath);
                Assert.Single(text);
                Assert.Equal("gal dir", text[0]);

                //
                // Use a handler that return the expected enum type.
                //
                options.AddToHistoryHandler = newAddToHistoryHandler_ReturnEnum;
                File.WriteAllText(newHistoryFilePath, string.Empty);
                SetHistory(commandInputs);

                historyItems = PSConsoleReadLine.GetHistoryItems();
                Assert.Equal(expectedQueuedItems.Length, historyItems.Length);
                for (int i = 0; i < expectedQueuedItems.Length; i++)
                {
                    Assert.Equal(expectedQueuedItems[i], historyItems[i].CommandLine);
                }

                text = File.ReadAllLines(newHistoryFilePath);
                Assert.Equal(expectedSavedItems.Length, text.Length);
                for (int i = 0; i < text.Length; i++)
                {
                    Assert.Equal(expectedSavedItems[i], text[i]);
                }

                //
                // Use a handler that return unexpected value.
                //   - same behavior as setting the handler to null.
                //
                options.AddToHistoryHandler = newAddToHistoryHandler_ReturnOther;
                File.WriteAllText(newHistoryFilePath, string.Empty);
                SetHistory(commandInputs);

                historyItems = PSConsoleReadLine.GetHistoryItems();
                Assert.Equal(commandInputs.Length, historyItems.Length);
                for (int i = 0; i < commandInputs.Length; i++)
                {
                    Assert.Equal(commandInputs[i], historyItems[i].CommandLine);
                }

                // All commands are saved to the history file when 'ScrubSensitiveHistory' is set to 'false'.
                text = File.ReadAllLines(newHistoryFilePath);
                Assert.Equal(commandInputs.Length, text.Length);
                for (int i = 0; i < text.Length; i++)
                {
                    Assert.Equal(commandInputs[i], text[i]);
                }
            }
            finally
            {
                options.HistorySavePath = oldHistoryFilePath;
                options.HistorySaveStyle = oldHistorySaveStyle;
                options.AddToHistoryHandler = PSConsoleReadLineOptions.DefaultAddToHistoryHandler;
                File.Delete(newHistoryFilePath);
            }
        }

        [SkippableFact]
        public void SensitiveHistoryOptionalBehaviorWithScriptBlock()
        {
            TestSetup(KeyMode.Cmd);

            // No history
            SetHistory();
            Test("", Keys(_.UpArrow, _.DownArrow));

            var options = PSConsoleReadLine.GetOptions();
            var oldHistoryFilePath = options.HistorySavePath;
            var oldHistorySaveStyle = options.HistorySaveStyle;

            // AddToHistoryHandler should be set to the default handler.
            Assert.Same(PSConsoleReadLineOptions.DefaultAddToHistoryHandler, options.AddToHistoryHandler);

            var newHistoryFilePath = Path.GetTempFileName();
            var newHistorySaveStyle = HistorySaveStyle.SaveIncrementally;
            Func<string, object> newAddToHistoryHandler_ReturnBool = LanguagePrimitives.ConvertTo<Func<string, object>>(
                ScriptBlock.Create(@"
                    param([string]$line)
                    $line.Contains('gal')"));
            Func<string, object> newAddToHistoryHandler_ReturnEnum = LanguagePrimitives.ConvertTo<Func<string, object>>(
                ScriptBlock.Create(@"
                    param([string]$line)
                    if ($line.Contains('gal')) {
                        [psobject]::AsPSObject([Microsoft.PowerShell.AddToHistoryOption]::MemoryOnly)
                    } elseif ($line.Contains('gmo')) {
                        'SkipAdding'
                    } else {
                        [Microsoft.PowerShell.AddToHistoryOption]::MemoryAndFile
                    }"));
            Func<string, object> newAddToHistoryHandler_ReturnOther = LanguagePrimitives.ConvertTo<Func<string, object>>(
                ScriptBlock.Create(@"
                    param([string]$line)
                    'string value'"));

            string[] commandInputs = new[] {
                "gmo p*",
                "gcm c*",
                "gal dir",
                "ConvertTo-SecureString -AsPlainText -String abc -Force"
            };

            string[] expectedQueuedItems = new[] {
                "gcm c*",
                "gal dir",
                "ConvertTo-SecureString -AsPlainText -String abc -Force"
            };

            string[] expectedSavedItems = new[] {
                "gcm c*",
                "ConvertTo-SecureString -AsPlainText -String abc -Force"
            };

            try
            {
                options.HistorySavePath = newHistoryFilePath;
                options.HistorySaveStyle = newHistorySaveStyle;

                //
                // Set null to the handler means we don't do the check.
                //
                options.AddToHistoryHandler = null;
                SetHistory(commandInputs);

                // All commands should be kept in the internal history queue.
                var historyItems = PSConsoleReadLine.GetHistoryItems();
                Assert.Equal(commandInputs.Length, historyItems.Length);
                for (int i = 0; i < commandInputs.Length; i++)
                {
                    Assert.Equal(commandInputs[i], historyItems[i].CommandLine);
                }

                // All commands are saved to the history file when 'ScrubSensitiveHistory' is set to 'false'.
                string[] text = File.ReadAllLines(newHistoryFilePath);
                Assert.Equal(commandInputs.Length, text.Length);
                for (int i = 0; i < text.Length; i++)
                {
                    Assert.Equal(commandInputs[i], text[i]);
                }

                //
                // Use a handler that return boolean value.
                //   true: Add to memory and file
                //   false: Skip adding to history
                //
                options.AddToHistoryHandler = newAddToHistoryHandler_ReturnBool;
                // Clear the history file.
                File.WriteAllText(newHistoryFilePath, string.Empty);
                SetHistory(commandInputs);

                historyItems = PSConsoleReadLine.GetHistoryItems();
                Assert.Single(historyItems);
                Assert.Equal("gal dir", historyItems[0].CommandLine);

                text = File.ReadAllLines(newHistoryFilePath);
                Assert.Single(text);
                Assert.Equal("gal dir", text[0]);

                //
                // Use a handler that return the expected enum type.
                //
                options.AddToHistoryHandler = newAddToHistoryHandler_ReturnEnum;
                File.WriteAllText(newHistoryFilePath, string.Empty);
                SetHistory(commandInputs);

                historyItems = PSConsoleReadLine.GetHistoryItems();
                Assert.Equal(expectedQueuedItems.Length, historyItems.Length);
                for (int i = 0; i < expectedQueuedItems.Length; i++)
                {
                    Assert.Equal(expectedQueuedItems[i], historyItems[i].CommandLine);
                }

                text = File.ReadAllLines(newHistoryFilePath);
                Assert.Equal(expectedSavedItems.Length, text.Length);
                for (int i = 0; i < text.Length; i++)
                {
                    Assert.Equal(expectedSavedItems[i], text[i]);
                }

                //
                // Use a handler that return unexpected value.
                //   - same behavior as setting the handler to null.
                //
                options.AddToHistoryHandler = newAddToHistoryHandler_ReturnOther;
                File.WriteAllText(newHistoryFilePath, string.Empty);
                SetHistory(commandInputs);

                historyItems = PSConsoleReadLine.GetHistoryItems();
                Assert.Equal(commandInputs.Length, historyItems.Length);
                for (int i = 0; i < commandInputs.Length; i++)
                {
                    Assert.Equal(commandInputs[i], historyItems[i].CommandLine);
                }

                // All commands are saved to the history file when 'ScrubSensitiveHistory' is set to 'false'.
                text = File.ReadAllLines(newHistoryFilePath);
                Assert.Equal(commandInputs.Length, text.Length);
                for (int i = 0; i < text.Length; i++)
                {
                    Assert.Equal(commandInputs[i], text[i]);
                }
            }
            finally
            {
                options.HistorySavePath = oldHistoryFilePath;
                options.HistorySaveStyle = oldHistorySaveStyle;
                options.AddToHistoryHandler = PSConsoleReadLineOptions.DefaultAddToHistoryHandler;
                File.Delete(newHistoryFilePath);
            }
        }

        [SkippableFact]
        public void HistoryRecallCurrentLine()
        {
            TestSetup(KeyMode.Cmd);

            SetHistory("echo foo", "echo bar");
            Test("ec", Keys("ec", _.UpArrow, _.UpArrow, _.DownArrow, _.DownArrow));
            Test("get", Keys("ec", _.UpArrow, _.DownArrow, _.Escape, "get", _.UpArrow, _.DownArrow));
            Test("", Keys("ec", _.UpArrow, _.DownArrow, _.Escape, "get", _.UpArrow, _.DownArrow, _.Escape));
        }

        [SkippableFact]
        public void HistorySearchCurrentLine()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("UpArrow", PSConsoleReadLine.HistorySearchBackward),
                      new KeyHandler("DownArrow", PSConsoleReadLine.HistorySearchForward));

            SetHistory("echo foo", "echo bar");
            Test("ec", Keys("ec", _.UpArrow, _.UpArrow, _.DownArrow, _.DownArrow));
            Test("echo ", Keys("ec", _.UpArrow, _.DownArrow, _.Escape, "echo ", _.UpArrow, _.DownArrow));
            Test("", Keys("ec", _.UpArrow, _.DownArrow, _.Escape, "echo ", _.UpArrow, _.DownArrow, _.Escape));
        }

        [SkippableFact]
        public void SearchHistory()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("UpArrow", PSConsoleReadLine.HistorySearchBackward),
                      new KeyHandler("DownArrow", PSConsoleReadLine.HistorySearchForward));

            // No history
            SetHistory();
            Test("", Keys(_.UpArrow, _.DownArrow));

            // Clear history in case the above added some history (but it shouldn't)
            SetHistory();
            Test(" ", Keys(' ', _.UpArrow, _.DownArrow));

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistorySearchCursorMovesToEnd = false});
            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);

            SetHistory("dosomething", "ps p*", "dir", "echo zzz");
            Test("dosomething", Keys(
                "d",
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "ir");
                    AssertCursorLeftIs(1);
                }),
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "osomething");
                    AssertCursorLeftIs(1);
            })));

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistorySearchCursorMovesToEnd = true});
            SetHistory("dosomething", "ps p*", "dir", "echo zzz");
            Test("dosomething", Keys(
                "d",
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "ir");
                    AssertCursorLeftIs(3);
                }),
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "osomething");
                    AssertCursorLeftIs(11);
                }),
                _.DownArrow, CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "ir");
                    AssertCursorLeftIs(3);
                }),
                _.UpArrow,   CheckThat(() =>
                {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "osomething");
                    AssertCursorLeftIs(11);
                })));
        }

        [SkippableFact]
        public void HistorySearchCursorMovesToEnd()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("UpArrow", PSConsoleReadLine.HistorySearchBackward),
                      new KeyHandler("DownArrow", PSConsoleReadLine.HistorySearchForward));

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistorySearchCursorMovesToEnd = true});
            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);

            SetHistory("dosomething", "ps p*", "dir", "echo zzz");
            Test("dosomething", Keys(
                "d",
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "ir");
                    AssertCursorLeftIs(3);
                }),
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "osomething");
                    AssertCursorLeftIs(11);
                }),
                _.DownArrow, CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "ir");
                    AssertCursorLeftIs(3);
                }),
                _.UpArrow,   CheckThat(() =>
                {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "osomething");
                    AssertCursorLeftIs(11);
                })));
        }

        [SkippableFact]
        public void BeginningOfHistory()
        {
            Skip.IfNot(KeyboardHasLessThan);

            TestSetup(KeyMode.Emacs);

            SetHistory("echo first", "echo second", "echo third");
            Test("echo first", Keys(_.Alt_Less));

            SetHistory("echo first", "echo second", "echo third");
            Test("echo second", Keys(_.Alt_Less, _.DownArrow));
        }

        [SkippableFact]
        public void EndOfHistory()
        {
            Skip.IfNot(KeyboardHasGreaterThan);

            TestSetup(KeyMode.Emacs);

            SetHistory("echo first", "echo second", "echo third");
            Test("", Keys(_.UpArrow, _.Alt_Greater));

            // Make sure end of history restores the "current" line if
            // there was anything entered before going through history
            Test("abc", Keys("abc", _.UpArrow, _.Alt_Greater));

            // Make sure we don't recall the previous "current" line
            // after we accepted it.
            Test("", Keys(_.Alt_Greater));
        }

        [SkippableFact]
        public void InteractiveHistorySearch()
        {
            TestSetup(KeyMode.Emacs);

            SetHistory("echo aaa");
            Test("echo aaa", Keys(_.Ctrl_r, 'a'));

            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
            var statusColors = Tuple.Create(_console.ForegroundColor, _console.BackgroundColor);

            // Test entering multiple characters and the line is updated with new matches
            SetHistory("zz1", "echo abc", "zz2", "echo abb", "zz3", "echo aaa", "zz4");
            Test("echo abc", Keys(_.Ctrl_r,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "aa",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                'b', CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "ab",
                    TokenClassification.None, 'b',
                    NextLine,
                    statusColors, "bck-i-search: ab_")),
                'c', CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "abc",
                    NextLine,
                    statusColors, "bck-i-search: abc_"))));

            // Test repeated Ctrl+r goes back through multiple matches
            SetHistory("zz1", "echo abc", "zz2", "echo abb", "zz3", "echo aaa", "zz4");
            Test("echo abc", Keys(_.Ctrl_r,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "aa",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.Ctrl_r, CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bb",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.Ctrl_r, CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_"))));

            // Test that the current match doesn't change when typing
            // additional characters, only emphasis should change.
            SetHistory("zz1", "echo abzz", "echo abc", "zz2");
            Test("echo abc", Keys(_.Ctrl_r,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                'b',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "ab",
                    TokenClassification.None, 'c',
                    NextLine,
                    statusColors, "bck-i-search: ab_"))));

            // Test that abort restores line state before Ctrl+r
            SetHistory("zz1", "echo abzz", "echo abc", "zz2");
            Test("echo zed", Keys("echo zed", _.Ctrl_r,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.Ctrl_g,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    TokenClassification.None, "zed",
                    NextLine))));

            // Test that a random function terminates the search and has an
            // effect on the line found in history
            SetHistory("zz1", "echo abzz", "echo abc", "zz2");
            Test("echo zed", Keys(_.Ctrl_r,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.Alt_d, "zed"));

            // Test that Escape terminates the search leaving the
            // cursor at the point in the match.
            SetHistory("zz1", "echo abzz", "echo abc", "zz2");
            Test("echo yabc", Keys(_.Ctrl_r,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.Escape, "y"));

            // Test entering multiple characters, then backspace, make sure we restore
            // the correct line
            SetHistory("zz1", "echo abc", "zz2", "echo abb", "zz3", "echo aaa", "zz4");
            Test("echo aaa", Keys(_.Ctrl_r,
                _.Backspace,  // Try backspace on empty search string
                "ab", CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "ab",
                    TokenClassification.None, 'b',
                    NextLine,
                    statusColors, "bck-i-search: ab_")),
                _.Backspace,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "aa",
                    NextLine,
                    statusColors, "bck-i-search: a_"))));

            SetHistory("zz1", "echo abzz", "echo abc", "zz2");
            Test("", Keys(_.Ctrl_r,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.Ctrl_r,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bzz",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.Ctrl_r,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    TokenClassification.None, "abzz",
                    NextLine,
                    statusColors, "failed-bck-i-search: a_")),
                _.Ctrl_s,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bzz",
                    NextLine,
                    statusColors, "fwd-i-search: a_")),
                _.Ctrl_g));

            // Test that searching works after a failed search
            SetHistory("echo aa1", "echo bb1", "echo bb2", "echo aa2");
            Test("echo aa1", Keys(_.Ctrl_r, "zz", _.Backspace, _.Backspace, "a1",
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " a",
                    emphasisColors, "a1",
                    NextLine,
                    statusColors, "bck-i-search: a1_"))
                ));

            // Test that searching works after backspace after a successful search
            SetHistory("echo aa1", "echo bb1", "echo bb2", "echo aa2");
            Test("echo aa2", Keys(
                _.Ctrl_r,
                "aa",
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "aa",
                    TokenClassification.None, "2",
                    NextLine,
                    statusColors, "bck-i-search: aa_")),
                _.Ctrl_r,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "aa",
                    TokenClassification.None, "1",
                    NextLine,
                    statusColors, "bck-i-search: aa_")),
                _.Backspace,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "a",
                    TokenClassification.None, "a2",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                'a', _.Ctrl_r,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "aa",
                    TokenClassification.None, "1",
                    NextLine,
                    statusColors, "bck-i-search: aa_")),
                _.Backspace));

            // TODO: long search line
            // TODO: start with Ctrl+s
            // TODO: "fast" typing in search where buffered keys after search is accepted
        }

        [SkippableFact]
        public void AddToHistoryHandler()
        {
            TestSetup(KeyMode.Cmd);
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {AddToHistoryHandler = s => s.StartsWith("z")});

            SetHistory("zzzz", "azzz");
            Test("zzzz", Keys(_.UpArrow));
        }

        [SkippableFact]
        public void HistoryNoDuplicates()
        {
            TestSetup(KeyMode.Cmd);
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistoryNoDuplicates = false});

            SetHistory("zzzz", "aaaa", "bbbb", "bbbb", "cccc");
            Assert.Equal(5, PSConsoleReadLine.GetHistoryItems().Length);
            Test("aaaa", Keys(Enumerable.Repeat(_.UpArrow, 4)));

            // Changing the option should affect existing history.
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistoryNoDuplicates = true});
            Test("zzzz", Keys(Enumerable.Repeat(_.UpArrow, 4)));

            SetHistory("aaaa", "bbbb", "bbbb", "cccc");
            Assert.Equal(3, PSConsoleReadLine.GetHistoryItems().Length);
            Test("aaaa", Keys(Enumerable.Repeat(_.UpArrow, 3)));

            SetHistory("aaaa", "bbbb", "bbbb", "cccc");
            Test("cccc", Keys(
                Enumerable.Repeat(_.UpArrow, 3),
                Enumerable.Repeat(_.DownArrow, 2)));


            // No history
            SetHistory();
            Test("", Keys(_.UpArrow, _.DownArrow));
        }

        [SkippableFact]
        public void HistorySearchNoDuplicates()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("UpArrow", PSConsoleReadLine.HistorySearchBackward),
                      new KeyHandler("DownArrow", PSConsoleReadLine.HistorySearchForward));

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistoryNoDuplicates = true});
            SetHistory("0000", "echo aaaa", "1111", "echo bbbb", "2222", "echo bbbb", "3333", "echo cccc", "4444");
            Test("echo aaaa", Keys("echo", Enumerable.Repeat(_.UpArrow, 3)));

            SetHistory("0000", "echo aaaa", "1111", "echo bbbb", "2222", "echo bbbb", "3333", "echo cccc", "4444");
            Test("echo cccc", Keys(
                "echo",
                Enumerable.Repeat(_.UpArrow, 3),
                Enumerable.Repeat(_.DownArrow, 2)));
        }

        [SkippableFact]
        public void InteractiveHistorySearchNoDuplicates()
        {
            TestSetup(KeyMode.Emacs);

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistoryNoDuplicates = true});
            SetHistory("0000", "echo aaaa", "1111", "echo bbbb", "2222", "echo bbbb", "3333", "echo cccc", "4444");
            Test("echo aaaa", Keys(
                _.Ctrl_r, "echo", _.Ctrl_r, _.Ctrl_r));

            SetHistory("0000", "echo aaaa", "1111", "echo bbbb", "2222", "echo bbbb", "3333", "echo cccc", "4444");
            Test("echo cccc", Keys(
                _.Ctrl_r, "echo", _.Ctrl_r, _.Ctrl_r, _.Ctrl_s, _.Ctrl_s));

            SetHistory("0000", "echo aaaa", "1111", "echo bbbb", "2222", "echo bbbb", "3333", "echo cccc", "4444");
            Test("echo aaaa", Keys(
                _.Ctrl_r, "echo", _.Ctrl_r, _.Ctrl_r, _.Ctrl_h, _.Ctrl_r, _.Ctrl_r));
        }

        [SkippableFact]
        public void HistoryCount()
        {
            TestSetup(KeyMode.Cmd);

            SetHistory("zzzz", "aaaa", "bbbb", "cccc");

            // There should be 4 items in history, the following should remove the
            // oldest history item.
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {MaximumHistoryCount = 3});
            Test("aaaa", Keys(Enumerable.Repeat(_.UpArrow, 4)));

            Test("zzzz", Keys("zzzz"));
            Test("aaaa", Keys("aaaa"));
            Test("bbbb", Keys("bbbb"));
            Test("cccc", Keys("cccc"));
            Test("aaaa", Keys(Enumerable.Repeat(_.UpArrow, 4)));
        }
    }
}
