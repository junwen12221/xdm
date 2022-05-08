﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gtk;
using System.IO;
using GLib;
using Application = Gtk.Application;
using IoPath = System.IO.Path;
using XDM.Core.Lib.Common;
using XDMApp;
using XDM.Core.Lib.Util;
using XDM.Core.Lib.Common;
using XDM.Common.UI;
using Newtonsoft.Json;
using Log = Serilog.Log;
using Translations;
using XDM.Core.Lib.UI;
using Menu = Gtk.Menu;
using MenuItem = Gtk.MenuItem;
using XDM.Core.Lib.Downloader;
using XDM.GtkUI.Dialogs.NewDownload;
using XDM.GtkUI.Dialogs.ProgressWindow;
using XDM.GtkUI.Utils;
using XDM.GtkUI.Dialogs.DownloadComplete;

namespace XDM.GtkUI
{
    public class AppWinPeer : Window, IAppWinPeer
    {
        private TreeStore categoryTreeStore;
        private TreeView categoryTree;
        private ListStore inprogressDownloadsStore, finishedDownloadsStore;
        private TreeView lvInprogress, lvFinished;
        private ScrolledWindow swInProgress, swFinished;
        private TreeModelFilter finishedDownloadFilter;
        private TreeModelFilter inprogressDownloadFilter;
        private string? searchKeyword;
        private Category? category;
        private ToolButton btnNew, btnDel, btnOpenFile, btnOpenFolder, btnResume, btnPause;
        private IButton newButton, deleteButton, pauseButton, resumeButton, openFileButton, openFolderButton;
        private IMenuItem[] menuItems;
        private Menu newDownloadMenu;
        private WindowGroup windowGroup;

        public IEnumerable<FinishedDownloadEntry> FinishedDownloads
        {
            get => GetAllFinishedDownloads();
            set => SetFinishedDownloads(value);
        }

        public IEnumerable<InProgressDownloadEntry> InProgressDownloads
        {
            get => GetAllInProgressDownloads();
            set => SetInProgressDownloads(value);
        }

        public IList<IInProgressDownloadRow> SelectedInProgressRows => GetSelectedInProgressDownloads();

        public IList<IFinishedDownloadRow> SelectedFinishedRows => GetSelectedFinishedDownloads();

        public IButton NewButton => this.newButton;

        public IButton DeleteButton => this.deleteButton;

        public IButton PauseButton => this.pauseButton;

        public IButton ResumeButton => this.resumeButton;

        public IButton OpenFileButton => this.openFileButton;

        public IButton OpenFolderButton => this.openFolderButton;

        public bool IsInProgressViewSelected => GetSelectedCategory() == 0;

        public IMenuItem[] MenuItems => this.menuItems;

        public Dictionary<string, IMenuItem> MenuItemMap { get; private set; }

        public event EventHandler ClipboardChanged;
        public event EventHandler InProgressContextMenuOpening;
        public event EventHandler FinishedContextMenuOpening;
        public event EventHandler SelectionChanged;
        public event EventHandler NewDownloadClicked;
        public event EventHandler YoutubeDLDownloadClicked;
        public event EventHandler BatchDownloadClicked;
        public event EventHandler SettingsClicked;
        public event EventHandler ClearAllFinishedClicked;
        public event EventHandler ExportClicked;
        public event EventHandler ImportClicked;
        public event EventHandler BrowserMonitoringButtonClicked;
        public event EventHandler BrowserMonitoringSettingsClicked;
        public event EventHandler UpdateClicked;
        public event EventHandler HelpClicked;
        public event EventHandler SupportPageClicked;
        public event EventHandler BugReportClicked;
        public event EventHandler CheckForUpdateClicked;
        public event EventHandler<CategoryChangedEventArgs> CategoryChanged;
        public event EventHandler SchedulerClicked;
        public event EventHandler MoveToQueueClicked;
        public event EventHandler DownloadListDoubleClicked;
        public event EventHandler WindowCreated;

        private const int FINISHED_DATA_INDEX = 3;
        private const int INPROGRESS_DATA_INDEX = 5;

        private Menu menuInProgress, menuFinished;

        public AppWinPeer() : base("Xtreme Download Manager")
        {
            SetDefaultSize(800, 500);
            SetPosition(WindowPosition.Center);
            DeleteEvent += AppWin1_DeleteEvent;
            this.windowGroup = new WindowGroup();
            this.windowGroup.AddWindow(this);

            var hbMain = new HBox();
            Add(hbMain);

            hbMain.PackStart(CreateCategoryTree(), false, true, 2);
            hbMain.PackStart(CreateMainPanel(), true, true, 1);

            categoryTreeStore!.GetIterFirst(out TreeIter iter);
            categoryTreeStore.IterNext(ref iter);
            categoryTree!.Selection.SelectIter(iter);

            CreateMenu();
        }

        private void CreateMenu()
        {
            menuItems = new IMenuItem[]
            {
                new MenuItemWrapper("pause",new MenuItem("Pause")),
                new MenuItemWrapper("resume",new MenuItem("Resume") ),
                new MenuItemWrapper("delete",new MenuItem("Delete")),
                new MenuItemWrapper("saveAs",new MenuItem("Save As")),
                new MenuItemWrapper("refresh",new MenuItem("Refresh link")),
                new MenuItemWrapper("showProgress",new MenuItem("Show progress")),
                new MenuItemWrapper("copyURL",new MenuItem("Copy URL")),
                new MenuItemWrapper("properties",new MenuItem("Properties")),
                new MenuItemWrapper("open",new MenuItem("Open")),
                new MenuItemWrapper("openFolder",new MenuItem("Open folder")),
                new MenuItemWrapper("deleteDownloads",new MenuItem("Delete downloads")),
                new MenuItemWrapper("copyURL1",new MenuItem("Copy URL")),
                new MenuItemWrapper("copyFile",new MenuItem("Copy file")),
                new MenuItemWrapper("properties1",new MenuItem("Properties")),
                new MenuItemWrapper("restart",new MenuItem("Restart")),
                new MenuItemWrapper("schedule",new MenuItem("Schedule")),
                new MenuItemWrapper("downloadAgain",new MenuItem("Download again"))
            };

            var dict = new Dictionary<string, IMenuItem>();
            foreach (var mi in menuItems)
            {
                dict[mi.Name] = mi;
            }

            this.MenuItemMap = dict;

            menuFinished = new Menu();
            menuFinished.Append(((MenuItemWrapper)dict["open"]).MenuItem);
            menuFinished.Append(((MenuItemWrapper)dict["openFolder"]).MenuItem);
            menuFinished.Append(((MenuItemWrapper)dict["deleteDownloads"]).MenuItem);
            menuFinished.Append(((MenuItemWrapper)dict["downloadAgain"]).MenuItem);
            menuFinished.Append(((MenuItemWrapper)dict["copyURL1"]).MenuItem);
            menuFinished.Append(((MenuItemWrapper)dict["copyFile"]).MenuItem);
            menuFinished.Append(((MenuItemWrapper)dict["properties1"]).MenuItem);
            menuFinished.ShowAll();

            menuInProgress = new Menu();
            menuInProgress.Append(((MenuItemWrapper)dict["pause"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["resume"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["delete"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["saveAs"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["refresh"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["restart"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["schedule"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["showProgress"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["copyURL"]).MenuItem);
            menuInProgress.Append(((MenuItemWrapper)dict["properties"]).MenuItem);
            menuInProgress.ShowAll();

            newDownloadMenu = new Menu();

            var menuNewDownload = new MenuItem(TextResource.GetText("LBL_NEW_DOWNLOAD"));
            menuNewDownload.Activated += MenuNewDownload_Click;

            var menuVideoDownload = new MenuItem(TextResource.GetText("LBL_VIDEO_DOWNLOAD"));
            menuVideoDownload.Activated += MenuVideoDownload_Click;

            var menuBatchDownload = new MenuItem(TextResource.GetText("MENU_BATCH_DOWNLOAD"));
            menuBatchDownload.Activated += MenuBatchDownload_Click;

            newDownloadMenu.Append(menuNewDownload);
            newDownloadMenu.Append(menuVideoDownload);
            newDownloadMenu.Append(menuBatchDownload);
            newDownloadMenu.ShowAll();
        }

        private void MenuBatchDownload_Click(object? sender, EventArgs e)
        {
            this.BatchDownloadClicked?.Invoke(sender, e);
        }

        private void MenuVideoDownload_Click(object? sender, EventArgs e)
        {
            this.YoutubeDLDownloadClicked?.Invoke(sender, e);
        }

        private void MenuNewDownload_Click(object? sender, EventArgs e)
        {
            this.NewDownloadClicked?.Invoke(sender, e);
        }

        private Widget CreateMainPanel()
        {
            var vbMain = new VBox();
            vbMain.PackStart(CreateToolbar(), false, false, 1);
            vbMain.PackStart(CreateInProgressListView(), true, true, 1);
            vbMain.PackStart(CreateFinishedListView(), true, true, 1);
            return vbMain;
        }

        private Widget CreateToolbar()
        {

            var toolbar = new Toolbar
            {
                Style = ToolbarStyle.BothHoriz
            };

            btnNew = new ToolButton(new Image(LoadSvg("links-line", 14)), TextResource.GetText("DESC_NEW")) { IsImportant = true, MarginStart = 0, MarginEnd = 0 };
            toolbar.Add(btnNew);
            btnDel = new ToolButton(new Image(LoadSvg("delete-bin-7-line", 14)), TextResource.GetText("DESC_DEL")) { IsImportant = true, MarginStart = 0, MarginEnd = 0 };
            toolbar.Add(btnDel);
            btnOpenFile = new ToolButton(new Image(LoadSvg("external-link-line", 14)), TextResource.GetText("CTX_OPEN_FILE")) { IsImportant = true, MarginStart = 0, MarginEnd = 0 };
            toolbar.Add(btnOpenFile);
            btnOpenFolder = new ToolButton(new Image(LoadSvg("folder-shared-line", 14)), TextResource.GetText("CTX_OPEN_FOLDER")) { IsImportant = true, MarginStart = 0, MarginEnd = 0 };
            toolbar.Add(btnOpenFolder);
            btnResume = new ToolButton(new Image(LoadSvg("play-line", 14)), TextResource.GetText("MENU_RESUME")) { IsImportant = true, MarginStart = 0, MarginEnd = 0 };
            toolbar.Add(btnResume);
            btnPause = new ToolButton(new Image(LoadSvg("pause-line", 14)), TextResource.GetText("MENU_PAUSE")) { IsImportant = true, MarginStart = 0, MarginEnd = 0 };
            toolbar.Add(btnPause);

            toolbar.Add(new ToolItem() { Expand = true });

            //toolbar.Add(new SeparatorToolItem() { Expand = true, Draw = false });
            var cont = new ToolItem() { MarginEnd = 3 };

            var searchEntry = new Entry() { WidthChars = 15, PlaceholderText = TextResource.GetText("LBL_SEARCH") };
            searchEntry.Activated += (a, b) =>
            {
                searchKeyword = searchEntry.Text;
                finishedDownloadFilter.Refilter();
            };
            cont.Add(searchEntry);
            toolbar.Add(cont);
            var btnMenu = new ToolButton(new Image(LoadSvg("menu-line", 14)), string.Empty) { IsImportant = false };
            toolbar.Add(btnMenu);

            newButton = new ButtonWrapper(this.btnNew);
            deleteButton = new ButtonWrapper(this.btnDel);
            pauseButton = new ButtonWrapper(this.btnPause);
            resumeButton = new ButtonWrapper(this.btnResume);
            openFileButton = new ButtonWrapper(this.btnOpenFile);
            openFolderButton = new ButtonWrapper(this.btnOpenFolder);

            return toolbar;
        }

        private Widget CreateCategoryTree()
        {
            string GetFontIcon(string name)
            {
                switch (name)
                {
                    case "CAT_DOCUMENTS":
                        return "file-text-line";
                    case "CAT_MUSIC":
                        return "file-music-line";
                    case "CAT_VIDEOS":
                        return "movie-line";
                    case "CAT_COMPRESSED":
                        return "file-zip-line";
                    case "CAT_PROGRAMS":
                        return "function-line";
                    default:
                        return "file-line";
                }
            }

            categoryTree = new TreeView()
            {
                HeadersVisible = false,
                ShowExpanders = false,
                LevelIndentation = 15
            };
            categoryTree.StyleContext.AddClass("dark");

            var cols = new TreeViewColumn();

            var cell1 = new CellRendererPixbuf();
            cell1.SetPadding(3, 5);
            cols.PackStart(cell1, false);
            cols.AddAttribute(cell1, "pixbuf", 0);

            var cell2 = new CellRendererText();
            cell2.SetPadding(0, 5);
            cols.PackStart(cell2, true);
            cols.AddAttribute(cell2, "text", 1);

            categoryTreeStore = new TreeStore(typeof(Gdk.Pixbuf), typeof(string), typeof(Category));
            categoryTreeStore.AppendValues(LoadSvg("arrow-down-line"), TextResource.GetText("ALL_UNFINISHED"));
            var iter = categoryTreeStore.AppendValues(LoadSvg("check-line"), TextResource.GetText("ALL_FINISHED"));

            foreach (var category in Config.Instance.Categories)
            {
                categoryTreeStore.AppendValues(iter, LoadSvg(GetFontIcon(category.Name), 20),
                    category.DisplayName, category);
            }

            categoryTree.AppendColumn(cols);
            categoryTree.Model = categoryTreeStore;
            categoryTree.Selection.Mode = SelectionMode.Browse;
            categoryTree.ExpandAll();
            categoryTree.Selection.Changed += OnCategoryChanged;

            var scrolledWindow = new ScrolledWindow
            {
                OverlayScrolling = true,
                Margin = 5,
                MarginEnd = 2
            };
            //scrolledWindow.Margin = 5;
            scrolledWindow.ShadowType = ShadowType.In;
            scrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            scrolledWindow.Add(categoryTree);
            scrolledWindow.SetSizeRequest(160, 200);
            return scrolledWindow;
        }

        private void OnCategoryChanged(object? sender, EventArgs e)
        {
            if (lvInprogress == null || lvFinished == null)
            {
                return;
            }
            var paths = categoryTree.Selection.GetSelectedRows();
            if (paths == null || paths.Length == 0)
            {
                return;
            }

            if (paths[0].Depth == 1)
            {
                var index = paths[0].Indices[0];
                if (index == 0)
                {
                    swInProgress.ShowAll();
                    swFinished.Hide();
                    category = null;
                    btnOpenFile.Visible = btnOpenFolder.Visible = false;
                    btnPause.Visible = btnResume.Visible = true;
                }
                else
                {
                    swFinished.ShowAll();
                    swInProgress.Hide();
                    category = null;
                    finishedDownloadFilter.Refilter();
                    btnOpenFile.Visible = btnOpenFolder.Visible = true;
                    btnPause.Visible = btnResume.Visible = false;
                }
            }
            else
            {
                swFinished.ShowAll();
                swInProgress.Hide();
                if (categoryTree.Selection.GetSelected(out ITreeModel model, out TreeIter iter))
                {
                    category = (Category)model.GetValue(iter, 2);
                }
                finishedDownloadFilter.Refilter();
            }
        }

        private Widget CreateInProgressListView()
        {
            inprogressDownloadsStore = new ListStore(typeof(string),        // file name
                typeof(string),                                             // date modified
                typeof(string),                                             // size
                typeof(int),                                                // progress
                typeof(string),                                             // status
                typeof(InProgressDownloadEntry)                             // download type
                );

            inprogressDownloadFilter = new TreeModelFilter(inprogressDownloadsStore, null);
            inprogressDownloadFilter.VisibleFunc = (model, iter) =>
            {
                var name = (string)model.GetValue(iter, 0);
                return Helpers.IsOfCategoryOrMatchesKeyword(name, searchKeyword, category);
            };

            var sortedStore = new TreeModelSort(inprogressDownloadFilter);

            sortedStore.SetSortFunc(0, (model, iter1, iter2) =>
            {
                Console.WriteLine("called");
                var t1 = (string)model.GetValue(iter1, 0);
                var t2 = (string)model.GetValue(iter2, 0);
                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;
                return t1.CompareTo(t2);
            });

            sortedStore.SetSortFunc(1, (model, iter1, iter2) =>
            {
                var t1 = (InProgressDownloadEntry)model.GetValue(iter1, 5);
                var t2 = (InProgressDownloadEntry)model.GetValue(iter2, 5);
                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;
                return t1.DateAdded.CompareTo(t2.DateAdded);
            });

            sortedStore.SetSortFunc(2, (model, iter1, iter2) =>
            {
                var t1 = (InProgressDownloadEntry)model.GetValue(iter1, 5);
                var t2 = (InProgressDownloadEntry)model.GetValue(iter2, 5);
                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;
                return t1.Size.CompareTo(t2.Size);
            });

            lvInprogress = new TreeView(sortedStore);
            lvInprogress.Selection.Mode = SelectionMode.Multiple;

            //File name column
            var fileNameColumn = new TreeViewColumn
            {
                Resizable = true,
                Reorderable = false,
                Title = "Name",
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = 200
            };

            var fileIconRenderer = new CellRendererPixbuf { };
            fileIconRenderer.SetPadding(5, 5);
            fileNameColumn.PackStart(fileIconRenderer, false);
            fileNameColumn.SetCellDataFunc(fileIconRenderer, new CellLayoutDataFunc(GetFileIcon));

            var fileNameRendererText = new CellRendererText();
            fileNameColumn.PackStart(fileNameRendererText, false);
            fileNameColumn.SetAttributes(fileNameRendererText, "text", 0);
            lvInprogress.AppendColumn(fileNameColumn);

            //Last modified column
            var lastModifiedRendererText = new CellRendererText();
            var lastModifiedColumn = new TreeViewColumn("Date added", lastModifiedRendererText, "text", 1)
            {
                Resizable = true,
                Reorderable = false,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = 100
            };
            lastModifiedColumn.SortColumnId = 1;
            lastModifiedColumn.SortOrder = SortType.Descending;
            lastModifiedColumn.SetAttributes(lastModifiedRendererText, "text", 1);
            lvInprogress.AppendColumn(lastModifiedColumn);


            //File size column
            var fileSizeRendererText = new CellRendererText();
            //fileSizeRendererText.Xalign = 1.0f;
            var fileSizeColumn = new TreeViewColumn
            {
                Resizable = true,
                Reorderable = false,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = 80,
                Title = "Size",
            };
            fileSizeColumn.PackStart(fileSizeRendererText, false);
            fileSizeColumn.SetAttributes(fileSizeRendererText, "text", 2);
            lvInprogress.AppendColumn(fileSizeColumn);

            //File progress column
            var fileRendererProgress = new CellRendererProgress()
            {
                //Text = "Downloading",
            };
            fileRendererProgress.SetPadding(5, 10);

            var progressColumn = new TreeViewColumn("Progress", fileRendererProgress, "value", 3)
            {
                Resizable = true,
                Reorderable = false,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = 80
            };
            progressColumn.SetAttributes(fileRendererProgress, "value", 3);
            lvInprogress.AppendColumn(progressColumn);

            //Download status column
            var statusRendererText = new CellRendererText();
            statusRendererText.SetPadding(5, 8);
            var statusColumn = new TreeViewColumn("Status", statusRendererText, "text", 4)
            {
                Resizable = true,
                Reorderable = false,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = 80
            };
            statusColumn.SetAttributes(statusRendererText, "text", 4);
            lvInprogress.AppendColumn(statusColumn);

            lvInprogress.Selection.Changed += (_, _) =>
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            };

            lvInprogress.ButtonReleaseEvent += (a, b) =>
            {
                if (b.Event.Type == Gdk.EventType.ButtonRelease && b.Event.Button == 3)
                {
                    InProgressContextMenuOpening?.Invoke(this, EventArgs.Empty);
                    menuInProgress.PopupAtPointer(b.Event);
                }
            };

            sortedStore.SetSortColumnId(1, SortType.Descending);

            swInProgress = new ScrolledWindow { OverlayScrolling = true, Margin = 5, MarginStart = 0, MarginTop = 0, ShadowType = ShadowType.In };
            swInProgress.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            swInProgress.Add(lvInprogress);
            //scrolledWindow.SetSizeRequest(200, 200);

            return swInProgress;
        }

        private Widget CreateFinishedListView()
        {
            finishedDownloadsStore = new ListStore(typeof(string),          // file name
                typeof(string),                                             // date modified
                typeof(string),                                             // size
                typeof(FinishedDownloadEntry)                               // download type
                );

            finishedDownloadFilter = new TreeModelFilter(finishedDownloadsStore, null);
            finishedDownloadFilter.VisibleFunc = (model, iter) =>
            {
                var name = (string)model.GetValue(iter, 0);
                return Helpers.IsOfCategoryOrMatchesKeyword(name, searchKeyword, category);
            };

            var sortedStore = new TreeModelSort(finishedDownloadFilter);

            sortedStore.SetSortFunc(0, (model, iter1, iter2) =>
            {
                Console.WriteLine("called");
                var t1 = (string)model.GetValue(iter1, 0);
                var t2 = (string)model.GetValue(iter2, 0);

                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;

                return t1.CompareTo(t2);
            });

            sortedStore.SetSortFunc(1, (model, iter1, iter2) =>
            {
                var t1 = (FinishedDownloadEntry)model.GetValue(iter1, 3);
                var t2 = (FinishedDownloadEntry)model.GetValue(iter2, 3);

                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;

                return t1.DateAdded.CompareTo(t2.DateAdded);
            });

            sortedStore.SetSortFunc(2, (model, iter1, iter2) =>
            {
                var t1 = (FinishedDownloadEntry)model.GetValue(iter1, 3);
                var t2 = (FinishedDownloadEntry)model.GetValue(iter2, 3);

                if (t1 == null && t2 == null) return 0;
                if (t1 == null) return 1;
                if (t2 == null) return 2;

                return t1.Size.CompareTo(t2.Size);
            });

            lvFinished = new TreeView(sortedStore);
            lvFinished.Selection.Mode = SelectionMode.Multiple;

            //File name column
            var fileNameColumn = new TreeViewColumn
            {
                Resizable = true,
                Reorderable = false,
                Title = "Name",
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = 200
            };

            var fileIconRenderer = new CellRendererPixbuf { };
            fileIconRenderer.SetPadding(5, 5);
            fileNameColumn.PackStart(fileIconRenderer, false);
            fileNameColumn.SetCellDataFunc(fileIconRenderer, new CellLayoutDataFunc(GetFileIcon));
            fileNameColumn.SortColumnId = 0;

            var fileNameRendererText = new CellRendererText();
            fileNameColumn.PackStart(fileNameRendererText, false);
            fileNameColumn.SetAttributes(fileNameRendererText, "text", 0);
            lvFinished.AppendColumn(fileNameColumn);

            //Last modified column
            var lastModifiedRendererText = new CellRendererText();
            var lastModifiedColumn = new TreeViewColumn("Date added", lastModifiedRendererText, "text", 1)
            {
                Resizable = true,
                Reorderable = false,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = 100
            };
            lastModifiedColumn.SetAttributes(lastModifiedRendererText, "text", 1);
            lastModifiedColumn.SortColumnId = 1;
            lvFinished.AppendColumn(lastModifiedColumn);


            //File size column
            var fileSizeRendererText = new CellRendererText();
            //fileSizeRendererText.Xalign = 1.0f;
            var fileSizeColumn = new TreeViewColumn
            {
                Resizable = true,
                Reorderable = false,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = 80,
                Title = "Size",
            };
            fileSizeColumn.PackStart(fileSizeRendererText, false);
            fileSizeColumn.SetAttributes(fileSizeRendererText, "text", 2);
            fileSizeColumn.SortColumnId = 2;
            lvFinished.AppendColumn(fileSizeColumn);

            lvFinished.Selection.Changed += (_, _) =>
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            };

            lvFinished.ButtonReleaseEvent += (a, b) =>
            {
                if (b.Event.Type == Gdk.EventType.ButtonRelease && b.Event.Button == 3)
                {
                    FinishedContextMenuOpening?.Invoke(this, EventArgs.Empty);
                    menuFinished.PopupAtPointer(b.Event);
                }
            };

            sortedStore.SetSortColumnId(1, SortType.Descending);

            swFinished = new ScrolledWindow { OverlayScrolling = true, Margin = 5, MarginStart = 0, MarginTop = 0, ShadowType = ShadowType.In };
            swFinished.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            swFinished.Add(lvFinished);
            return swFinished;
        }

        void GetFileIcon(ICellLayout cell_layout,
                CellRenderer cell, ITreeModel tree_model, TreeIter iter)
        {
            var name = (string)tree_model.GetValue(iter, 0);
            var pix = LoadSvg(IconResource.GetSVGNameForFileType(name), 20);
            ((CellRendererPixbuf)cell).Pixbuf = pix;
        }

        private void AppWin1_DeleteEvent(object o, DeleteEventArgs args)
        {
            Application.Quit();
        }

        private static Gdk.Pixbuf LoadSvg(string name, int dimension = 16)
        {
            return GtkHelper.LoadSvg(name, dimension);
            //new Gdk.Pixbuf(
            //    IoPath.Combine(
            //        AppDomain.CurrentDomain.BaseDirectory, "svg-icons", $"{name}.svg"), dimension, dimension, true);
        }

        public IInProgressDownloadRow? FindInProgressItem(string id)
        {
            if (!inprogressDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                return null;
            }
            do
            {
                var ent = (InProgressDownloadEntry)inprogressDownloadsStore.GetValue(iter, INPROGRESS_DATA_INDEX);
                if (ent.Id == id)
                {
                    return new InProgressEntryWrapper(ent, iter, inprogressDownloadsStore);
                }
            }
            while (inprogressDownloadsStore.IterNext(ref iter));
            return null;
        }


        public IFinishedDownloadRow? FindFinishedItem(string id)
        {
            if (!this.finishedDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                return null;
            }
            do
            {
                var ent = (FinishedDownloadEntry)finishedDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
                if (ent.Id == id)
                {
                    return new FinishedEntryWrapper(ent, iter, finishedDownloadsStore);
                }
            }
            while (finishedDownloadsStore.IterNext(ref iter));
            return null;
        }

        public void AddToTop(InProgressDownloadEntry entry)
        {
            var iter = inprogressDownloadsStore.Insert(0);
            inprogressDownloadsStore.SetValue(iter, 0, entry.Name);
            inprogressDownloadsStore.SetValue(iter, 1, entry.DateAdded.ToShortDateString());
            inprogressDownloadsStore.SetValue(iter, 2, Helpers.FormatSize(entry.Size));
            inprogressDownloadsStore.SetValue(iter, 3, entry.Progress);
            inprogressDownloadsStore.SetValue(iter, 4, entry.Status);
            inprogressDownloadsStore.SetValue(iter, 5, entry);
        }

        public void AddToTop(FinishedDownloadEntry entry)
        {
            finishedDownloadsStore.AppendValues(
                entry.Name,
                entry.DateAdded.ToShortDateString(),
                Helpers.FormatSize(entry.Size));
        }

        public void SwitchToInProgressView()
        {
            if (this.categoryTreeStore.GetIterFirst(out TreeIter iter))
            {
                this.categoryTree.Selection.SelectIter(iter);
            }
        }

        public void ClearInProgressViewSelection()
        {
            this.lvInprogress.Selection.UnselectAll();
        }

        public void SwitchToFinishedView()
        {
            if (this.categoryTreeStore.GetIterFirst(out TreeIter iter) &&
                this.categoryTreeStore.IterNext(ref iter))
            {
                this.categoryTree.Selection.SelectIter(iter);
            }
        }

        public void ClearFinishedViewSelection()
        {
            this.lvFinished.Selection.UnselectAll();
        }

        public bool Confirm(object? window, string text)
        {
            if (window is not Window owner)
            {
                owner = this;
            }
            using var msg = new MessageDialog(owner, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo, text);
            if (msg.Run() == (int)ResponseType.Yes)
            {
                return true;
            }
            return false;
        }

        public IDownloadCompleteDialog CreateDownloadCompleteDialog(IApp app)
        {
            var win = DownloadCompleteDialog.CreateFromGladeFile();
            win.App = app;
            return win;
        }

        public INewDownloadDialogSkeleton CreateNewDownloadDialog(bool empty)
        {
            var window = NewDownloadWindow.CreateFromGladeFile();
            window.IsEmpty = empty;
            return window;
        }

        public INewVideoDownloadDialog CreateNewVideoDialog()
        {
            throw new NotImplementedException();
        }

        public IProgressWindow CreateProgressWindow(string downloadId, IApp app, IAppUI appUI)
        {
            var prgWin = DownloadProgressWindow.CreateFromGladeFile();
            prgWin.DownloadId = downloadId;
            prgWin.App = app;
            prgWin.AppUI = appUI;
            return prgWin;
        }

        public void RunOnUIThread(System.Action action)
        {
            Application.Invoke((a, b) => action.Invoke());
        }

        public void RunOnUIThread(Action<string, int, double, long> action, string id, int progress, double speed, long eta)
        {
            Application.Invoke((a, b) => action.Invoke(id, progress, speed, eta));
        }

        public void Delete(IInProgressDownloadRow row)
        {
            var iter = ((InProgressEntryWrapper)row).GetTreeIter();
            inprogressDownloadsStore.Remove(ref iter);
        }

        public void Delete(IFinishedDownloadRow row)
        {
            throw new NotImplementedException();
        }

        public void DeleteAllFinishedDownloads()
        {
            throw new NotImplementedException();
        }

        public void Delete(IEnumerable<IInProgressDownloadRow> rows)
        {
            throw new NotImplementedException();
        }

        public void Delete(IEnumerable<IFinishedDownloadRow> rows)
        {
            throw new NotImplementedException();
        }

        public string GetUrlFromClipboard()
        {
            //var cb = Clipboard.GetDefault(this.Display);
            //cb.
            return null;
        }

        public AuthenticationInfo? PromtForCredentials(string message)
        {
            throw new NotImplementedException();
        }

        public void ShowUpdateAvailableNotification()
        {
            //throw new NotImplementedException();
        }

        public void ShowMessageBox(object? window, string message)
        {
            if (window is not Window owner)
            {
                owner = this;
            }
            throw new NotImplementedException();
        }

        public void OpenNewDownloadMenu()
        {
            newDownloadMenu.PopupAtWidget(this.btnNew, Gdk.Gravity.SouthWest, Gdk.Gravity.NorthWest, null);
        }

        public string? SaveFileDialog(string? initialPath)
        {
            throw new NotImplementedException();
        }

        public void ShowRefreshLinkDialog(InProgressDownloadEntry entry, IApp app)
        {
            throw new NotImplementedException();
        }

        public void SetClipboardText(string text)
        {
            throw new NotImplementedException();
        }

        public void SetClipboardFile(string file)
        {
            throw new NotImplementedException();
        }

        public void ShowPropertiesDialog(BaseDownloadEntry ent, ShortState? state)
        {
            throw new NotImplementedException();
        }

        public void ShowYoutubeDLDialog(IAppUI appUI, IApp app)
        {
            throw new NotImplementedException();
        }

        public DownloadSchedule? ShowSchedulerDialog(DownloadSchedule schedule)
        {
            throw new NotImplementedException();
        }

        public void ShowBatchDownloadWindow(IApp app)
        {
            throw new NotImplementedException();
        }

        public void ShowSettingsDialog(IApp app, int page = 0)
        {
            throw new NotImplementedException();
        }

        public void ImportDownloads(IApp app)
        {
            throw new NotImplementedException();
        }

        public void ExportDownloads(IApp app)
        {
            throw new NotImplementedException();
        }

        public void UpdateBrowserMonitorButton()
        {
            throw new NotImplementedException();
        }

        public void ShowBrowserMonitoringDialog(IApp app)
        {
            throw new NotImplementedException();
        }

        public void UpdateParallalismLabel()
        {
            throw new NotImplementedException();
        }

        public IUpdaterUI CreateUpdateUIDialog(IAppUI ui)
        {
            throw new NotImplementedException();
        }

        public void ClearUpdateInformation()
        {
            throw new NotImplementedException();
        }

        private IEnumerable<FinishedDownloadEntry> GetAllFinishedDownloads()
        {
            if (!finishedDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                yield break;
            }
            yield return (FinishedDownloadEntry)finishedDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
            while (finishedDownloadsStore.IterNext(ref iter))
            {
                yield return (FinishedDownloadEntry)finishedDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
            }
        }

        private IEnumerable<InProgressDownloadEntry> GetAllInProgressDownloads()
        {
            if (!inprogressDownloadsStore!.GetIterFirst(out TreeIter iter))
            {
                yield break;
            }
            yield return (InProgressDownloadEntry)inprogressDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
            while (inprogressDownloadsStore.IterNext(ref iter))
            {
                yield return (InProgressDownloadEntry)inprogressDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
            }
        }

        private void SetFinishedDownloads(IEnumerable<FinishedDownloadEntry> finishedDownloads)
        {
            finishedDownloadsStore.Clear();
            foreach (var item in finishedDownloads)
            {
                finishedDownloadsStore.AppendValues(item.Name,
                    item.DateAdded.ToShortDateString(),
                    Helpers.FormatSize(item.Size),
                    item);
            }
        }

        private void SetInProgressDownloads(IEnumerable<InProgressDownloadEntry> incompleteDownloads)
        {
            inprogressDownloadsStore.Clear();
            foreach (var item in incompleteDownloads)
            {
                inprogressDownloadsStore.AppendValues(item.Name,
                    item.DateAdded.ToShortDateString(),
                    Helpers.FormatSize(item.Size),
                    item.Progress,
                    Helpers.GenerateStatusText(item),
                    item);
            }
        }

        private IList<IInProgressDownloadRow> GetSelectedInProgressDownloads()
        {
            var list = new List<IInProgressDownloadRow>(0);
            var rows = lvInprogress.Selection.GetSelectedRows(out ITreeModel model);
            if (rows != null && rows.Length > 0)
            {
                list.Capacity = rows.Length;
                foreach (var row in rows)
                {
                    if (model.GetIter(out TreeIter iter, row))
                    {
                        var ent = (InProgressDownloadEntry)model.GetValue(iter, INPROGRESS_DATA_INDEX);
                        list.Add(new InProgressEntryWrapper(ent, iter, model));
                    }
                }
            }
            return list;
        }

        private IList<IFinishedDownloadRow> GetSelectedFinishedDownloads()
        {
            var list = new List<IFinishedDownloadRow>(0);
            var rows = lvFinished.Selection.GetSelectedRows(out ITreeModel model);
            if (rows != null && rows.Length > 0)
            {
                list.Capacity = rows.Length;
                foreach (var row in rows)
                {
                    if (model.GetIter(out TreeIter iter, row))
                    {
                        var ent = (FinishedDownloadEntry)model.GetValue(iter, FINISHED_DATA_INDEX);
                        list.Add(new FinishedEntryWrapper(ent, iter, model));
                    }
                }
            }
            return list;
        }

        private int GetSelectedCategory()
        {
            var paths = categoryTree.Selection.GetSelectedRows();
            if (paths != null && paths.Length > 0 && paths[0].Depth == 1)
            {
                return paths[0].Indices[0];
            }
            return -1;
        }

        public void ShowQueuesAndSchedulerWindow()
        {
            throw new NotImplementedException();
        }

        public IQueuesWindow CreateQueuesAndSchedulerWindow(IAppUI appUi, IEnumerable<DownloadQueue> queues)
        {
            throw new NotImplementedException();
        }

        public IQueueSelectionDialog CreateQueueSelectionDialog()
        {
            throw new NotImplementedException();
        }

        public void ConfirmDelete(string text, out bool approved, out bool deleteFiles)
        {
            Log.Debug(text);
            throw new NotImplementedException();
        }

        public string? SaveFileDialog(string? initialPath, string? defaultExt, string? filter)
        {
            throw new NotImplementedException();
        }

        public string? OpenFileDialog(string? initialPath, string? defaultExt, string? filter)
        {
            throw new NotImplementedException();
        }

        public void ShowBatchDownloadWindow(IApp app, IAppUI appUi)
        {
            throw new NotImplementedException();
        }

        public IQueuesWindow CreateQueuesAndSchedulerWindow(IAppUI appUi)
        {
            throw new NotImplementedException();
        }

        public void ShowDownloadSelectionWindow(IApp app, IAppUI appUI, FileNameFetchMode mode, IEnumerable<object> downloads)
        {
            throw new NotImplementedException();
        }

        public IClipboardMonitor GetClipboardMonitor()
        {
            throw new NotImplementedException();
        }

        public void ShowFloatingWidget()
        {
            throw new NotImplementedException();
        }

        //private ref InProgressEntryWrapper? FindInProgressDownloadById()
        //{
        //    if (!inprogressDownloadsStore!.GetIterFirst(out TreeIter iter))
        //    {
        //        return null;
        //    }

        //    var ent=(InProgressDownloadEntry)inprogressDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
        //    while (inprogressDownloadsStore.IterNext(ref iter))
        //    {
        //         return (InProgressDownloadEntry)inprogressDownloadsStore.GetValue(iter, FINISHED_DATA_INDEX);
        //    }
        //}
    }


}