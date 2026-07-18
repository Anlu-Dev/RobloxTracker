using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace RobloxTracker
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient Client = CreateHttpClient();

        private static readonly string SettingsFolder =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "RobloxTracker");

        private static readonly string SettingsFile =
            Path.Combine(SettingsFolder, "settings.json");

        private const string InventoryAssetTypes =
            "Hat,HairAccessory,FaceAccessory,NeckAccessory," +
            "ShoulderAccessory,FrontAccessory,BackAccessory," +
            "WaistAccessory,Shirt,Pants,TShirt,Gear,Head,Face," +
            "EmoteAnimation,TShirtAccessory,ShirtAccessory," +
            "PantsAccessory,JacketAccessory,SweaterAccessory," +
            "ShortsAccessory,DressSkirtAccessory," +
            "LeftShoeAccessory,RightShoeAccessory,DynamicHead";

        private readonly List<InventoryItem> _inventoryItems = new();
        private readonly Dictionary<long, string> _inventoryThumbnails = new();

        private readonly List<GroupMembership> _groups = new();
        private readonly Dictionary<long, string> _groupIcons = new();

        private AppSettings _settings = new();
        private UserDetails? _currentUser;

        private long? _currentUserId;
        private long? _loadedFriendsForUserId;
        private long? _loadedInventoryForUserId;
        private long? _loadedGroupsForUserId;

        private string? _inventoryCursor;
        private bool _isLoadingInventory;
        private bool _autoConnectAttempted;
        private bool _isApplyingSettings;
        private bool _isUiReady;

        public MainWindow()
        {
            InitializeComponent();
            _isUiReady = true;

            LoadSettings();
            ApplySettingsToControls();

            Loaded += MainWindow_Loaded;
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "RobloxTracker/1.7");

            return client;
        }

        private async void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_autoConnectAttempted)
                return;

            _autoConnectAttempted = true;

            if (_settings.RememberUsername &&
                _settings.AutoConnect &&
                !string.IsNullOrWhiteSpace(_settings.LastUsername))
            {
                UsernameInput.Text = _settings.LastUsername;

                await Task.Delay(400);
                await SearchUserAsync();
            }
        }

        private async void SearchButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await SearchUserAsync();
        }

        private async void UsernameInput_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await SearchUserAsync();
        }

        private void DashboardNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowDashboard();
        }

        private async void SocialNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!EnsureAccountConnected())
                return;

            ShowSocial();
            await LoadFriendsAsync();
        }

        private async void InventoryNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!EnsureAccountConnected())
                return;

            ShowInventory();

            if (_loadedInventoryForUserId != _currentUserId)
                await LoadInventoryAsync(true);
        }

        private async void GroupsNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!EnsureAccountConnected())
                return;

            ShowGroups();

            if (_loadedGroupsForUserId != _currentUserId)
                await LoadGroupsAsync();
        }

        private void SettingsNavButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSettings();
        }

        private async void LoadMoreInventoryButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadInventoryAsync(false);
        }

        private void InventoryFilterChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (IsInitialized && InventoryWrapPanel != null)
                RenderInventoryItems();
        }

        private void GroupSearchInput_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (IsInitialized && GroupsWrapPanel != null)
                RenderGroups();
        }

        private void ApiDelaySlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (ApiDelayValueText != null)
            {
                ApiDelayValueText.Text =
                    IsKorean
                        ? $"{e.NewValue / 1000:0.0}초"
                        : $"{e.NewValue / 1000:0.0}s";
            }
        }

        private void LanguageComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!_isUiReady ||
                _isApplyingSettings ||
                LanguageComboBox == null)
            {
                return;
            }

            _settings.Language =
                GetComboBoxTag(LanguageComboBox, "ko");

            ApplyLanguage();
            WriteSettings();

            SettingsStatusText.Foreground =
                CreateBrush("#60D394");

            SettingsStatusText.Text =
                T("언어를 변경했습니다.", "Language changed.");
        }

        private void RememberUsernameCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (AutoConnectCheckBox == null)
                return;

            bool remember =
                RememberUsernameCheckBox.IsChecked == true;

            AutoConnectCheckBox.IsEnabled = remember;

            if (!remember)
                AutoConnectCheckBox.IsChecked = false;
        }

        private void SaveSettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ReadSettingsFromControls();

            if (_settings.RememberUsername)
            {
                _settings.LastUsername =
                    UsernameInput.Text.Trim();
            }
            else
            {
                _settings.LastUsername = "";
            }

            if (WriteSettings())
            {
                SettingsStatusText.Foreground =
                    CreateBrush("#60D394");

                SettingsStatusText.Text =
                    T("설정을 저장했습니다.", "Settings saved.");
            }
            else
            {
                SettingsStatusText.Foreground =
                    CreateBrush("#FF5C5C");

                SettingsStatusText.Text =
                    T(
                        "설정을 저장하지 못했습니다.",
                        "Could not save settings.");
            }
        }

        private void ResetSettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _settings = new AppSettings();
            ApplySettingsToControls();
            WriteSettings();

            SettingsStatusText.Foreground =
                CreateBrush("#FFC857");

            SettingsStatusText.Text =
                T(
                    "기본 설정으로 초기화했습니다.",
                    "Settings were reset to default.");
        }

        private bool EnsureAccountConnected()
        {
            if (_currentUserId != null)
                return true;

            StatusText.Text = T(
                "먼저 Roblox 계정을 연결하세요.",
                "Connect a Roblox account first.");
            ShowDashboard();
            return false;
        }

        private void HideAllPanels()
        {
            DashboardPanel.Visibility = Visibility.Collapsed;
            SocialPanel.Visibility = Visibility.Collapsed;
            InventoryPanel.Visibility = Visibility.Collapsed;
            GroupsPanel.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowDashboard()
        {
            HideAllPanels();
            DashboardPanel.Visibility = Visibility.Visible;
            SetActiveNavigation(DashboardNavButton);
        }

        private void ShowSocial()
        {
            HideAllPanels();
            SocialPanel.Visibility = Visibility.Visible;
            SetActiveNavigation(SocialNavButton);
        }

        private void ShowInventory()
        {
            HideAllPanels();
            InventoryPanel.Visibility = Visibility.Visible;
            SetActiveNavigation(InventoryNavButton);
        }

        private void ShowGroups()
        {
            HideAllPanels();
            GroupsPanel.Visibility = Visibility.Visible;
            SetActiveNavigation(GroupsNavButton);
        }

        private void ShowSettings()
        {
            HideAllPanels();
            SettingsPanel.Visibility = Visibility.Visible;
            SetActiveNavigation(SettingsNavButton);
        }

        private void SetActiveNavigation(Button activeButton)
        {
            Button[] buttons =
            {
                DashboardNavButton,
                SocialNavButton,
                InventoryNavButton,
                GroupsNavButton,
                SettingsNavButton
            };

            foreach (Button button in buttons)
            {
                bool active = button == activeButton;

                button.Background = active
                    ? CreateBrush("#202632")
                    : Brushes.Transparent;

                button.Foreground = active
                    ? Brushes.White
                    : CreateBrush("#A9B0BF");
            }
        }

        private async Task SearchUserAsync()
        {
            string username = UsernameInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                StatusText.Text = T(
                    "사용자명을 입력하세요.",
                    "Enter a username.");
                return;
            }

            ShowDashboard();
            SetLoading(true);

            try
            {
                HttpResponseMessage lookupResponse =
                    await Client.PostAsJsonAsync(
                        "https://users.roblox.com/v1/usernames/users",
                        new
                        {
                            usernames = new[] { username },
                            excludeBannedUsers = false
                        });

                lookupResponse.EnsureSuccessStatusCode();

                UsernameLookupResponse? lookupData =
                    await lookupResponse.Content
                        .ReadFromJsonAsync<UsernameLookupResponse>();

                LookupUser? foundUser =
                    lookupData?.Data.FirstOrDefault();

                if (foundUser == null)
                {
                    ResetProfile();
                    StatusText.Text = T(
                        "존재하지 않는 사용자입니다.",
                        "That user does not exist.");
                    return;
                }

                UserDetails? user =
                    await Client.GetFromJsonAsync<UserDetails>(
                        $"https://users.roblox.com/v1/users/{foundUser.Id}");

                if (user == null)
                {
                    throw new Exception(T(
                        "사용자 정보를 불러오지 못했습니다.",
                        "Could not load user information."));
                }

                Task<string> friendsTask = GetCountAsync(
                    $"https://friends.roblox.com/v1/users/{user.Id}/friends/count");

                Task<string> followersTask = GetCountAsync(
                    $"https://friends.roblox.com/v1/users/{user.Id}/followers/count");

                Task<string> followingTask = GetCountAsync(
                    $"https://friends.roblox.com/v1/users/{user.Id}/followings/count");

                Task<string?> avatarTask =
                    GetAvatarUrlAsync(user.Id);

                await Task.WhenAll(
                    friendsTask,
                    followersTask,
                    followingTask,
                    avatarTask);

                _currentUserId = user.Id;
                _currentUser = user;
                _loadedFriendsForUserId = null;
                _loadedInventoryForUserId = null;
                _loadedGroupsForUserId = null;
                _inventoryCursor = null;

                _inventoryItems.Clear();
                _inventoryThumbnails.Clear();
                _groups.Clear();
                _groupIcons.Clear();

                FriendsWrapPanel.Children.Clear();
                InventoryWrapPanel.Children.Clear();
                GroupsWrapPanel.Children.Clear();

                DisplayNameText.Text = user.DisplayName;
                UsernameText.Text = $"@{user.Name}";
                UserIdText.Text = user.Id.ToString();

                CreatedText.Text = FormatCreatedDate(user.Created);

                AccountAgeText.Text = GetAccountAge(user.Created);

                FriendsText.Text = await friendsTask;
                FollowersText.Text = await followersTask;
                FollowingText.Text = await followingTask;

                SocialFriendsText.Text = FriendsText.Text;
                SocialFollowersText.Text = FollowersText.Text;
                SocialFollowingText.Text = FollowingText.Text;

                UpdateUserSectionTitles(user.DisplayName);

                DescriptionText.Text =
                    string.IsNullOrWhiteSpace(user.Description)
                        ? T("소개글이 없습니다.", "No description.")
                        : user.Description;

                VerifiedBadge.Visibility =
                    user.HasVerifiedBadge
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                AccountStatusText.Text =
                    user.IsBanned
                        ? T("이용 정지", "BANNED")
                        : T("활성", "ACTIVE");

                AccountStatusText.Foreground =
                    CreateBrush(
                        user.IsBanned
                            ? "#FF5C5C"
                            : "#60D394");

                AccountStatusBorder.Background =
                    CreateBrush(
                        user.IsBanned
                            ? "#411C1F"
                            : "#173A2B");

                string? avatarUrl = await avatarTask;

                if (!string.IsNullOrWhiteSpace(avatarUrl))
                    AvatarImage.Source = CreateBitmap(avatarUrl, 420);

                StatusText.Text =
                    IsKorean
                        ? $"{user.DisplayName} 계정을 연결했습니다."
                        : $"Connected to {user.DisplayName}.";

                if (_settings.RememberUsername)
                {
                    _settings.LastUsername = user.Name;
                    WriteSettings();
                }

                await OpenDefaultTabAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    IsTooManyRequests(ex as HttpRequestException)
                        ? T(
                            "Roblox 요청이 너무 많습니다. 잠시 후 다시 시도해주세요.",
                            "Too many Roblox requests. Please try again shortly.")
                        : IsKorean
                            ? $"연결 오류: {ex.Message}"
                            : $"Connection error: {ex.Message}";
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async Task OpenDefaultTabAsync()
        {
            switch (_settings.DefaultTab)
            {
                case "Social":
                    ShowSocial();
                    await LoadFriendsAsync();
                    break;

                case "Groups":
                    ShowGroups();
                    await LoadGroupsAsync();
                    break;

                case "Inventory":
                    ShowInventory();
                    await LoadInventoryAsync(true);
                    break;

                default:
                    ShowDashboard();
                    break;
            }
        }

        private async Task LoadFriendsAsync()
        {
            if (_currentUserId == null ||
                _loadedFriendsForUserId == _currentUserId)
            {
                return;
            }

            long userId = _currentUserId.Value;

            FriendsWrapPanel.Children.Clear();
            FriendsListStatusText.Text = T(
                "친구 목록을 불러오는 중...",
                "Loading friends...");

            try
            {
                await SafeDelayAsync();

                FriendsResponse? response =
                    await Client.GetFromJsonAsync<FriendsResponse>(
                        $"https://friends.roblox.com/v1/users/{userId}/friends");

                FriendUser[] friends =
                    response?.Data ?? Array.Empty<FriendUser>();

                Dictionary<long, FriendUser> profiles =
                    await GetUserProfilesAsync(
                        friends.Select(friend => friend.Id));

                friends = friends
                    .Select(friend =>
                        profiles.TryGetValue(friend.Id, out FriendUser? fresh)
                            ? fresh
                            : friend)
                    .OrderBy(friend => friend.DisplayName)
                    .ToArray();

                Dictionary<long, string> avatars =
                    await GetAvatarMapAsync(
                        friends.Select(friend => friend.Id));

                foreach (FriendUser friend in friends)
                {
                    avatars.TryGetValue(friend.Id, out string? avatar);

                    FriendsWrapPanel.Children.Add(
                        CreatePersonCard(friend, avatar));
                }

                FriendsListStatusText.Text =
                    IsKorean
                        ? $"{friends.Length:N0}명의 친구"
                        : $"{friends.Length:N0} friends";

                _loadedFriendsForUserId = userId;
            }
            catch
            {
                FriendsListStatusText.Text =
                    T(
                        "친구 목록을 불러오지 못했습니다.",
                        "Could not load friends.");
            }
        }

        private Border CreatePersonCard(
            FriendUser friend,
            string? avatarUrl)
        {
            Border card = CreateBaseCard();
            StackPanel content = new StackPanel();

            content.Children.Add(
                CreateImageBox(avatarUrl, true));

            content.Children.Add(
                CreateItemTitle(
                    string.IsNullOrWhiteSpace(friend.DisplayName)
                        ? friend.Name
                        : friend.DisplayName));

            content.Children.Add(new TextBlock
            {
                Text = $"@{friend.Name}",
                Foreground = CreateBrush("#858A96"),
                FontSize = 12,
                Margin = new Thickness(2, 2, 2, 0)
            });

            Button button = CreateCardButton(
                T("친구 추가", "ADD FRIEND"));

            button.Click += (_, _) =>
                OpenWebPage(
                    $"https://www.roblox.com/users/{friend.Id}/profile");

            content.Children.Add(button);
            card.Child = content;

            return card;
        }

        private async Task LoadGroupsAsync()
        {
            if (_currentUserId == null ||
                _loadedGroupsForUserId == _currentUserId)
            {
                return;
            }

            long userId = _currentUserId.Value;

            GroupsStatusText.Text = T(
                "그룹을 불러오는 중...",
                "Loading groups...");
            GroupsWrapPanel.Children.Clear();

            try
            {
                await SafeDelayAsync();

                GroupsResponse? response =
                    await Client.GetFromJsonAsync<GroupsResponse>(
                        $"https://groups.roblox.com/v1/users/{userId}/groups/roles");

                GroupMembership[] memberships =
                    response?.Data ?? Array.Empty<GroupMembership>();

                _groups.Clear();
                _groups.AddRange(memberships);

                await SafeDelayAsync(0.4);

                Dictionary<long, string> icons =
                    await GetGroupIconMapAsync(
                        memberships.Select(item => item.Group.Id));

                _groupIcons.Clear();

                foreach (var pair in icons)
                    _groupIcons[pair.Key] = pair.Value;

                _loadedGroupsForUserId = userId;
                RenderGroups();
            }
            catch
            {
                GroupsStatusText.Text =
                    T(
                        "그룹을 불러오지 못했습니다.",
                        "Could not load groups.");
            }
        }

        private void RenderGroups()
        {
            string search =
                GroupSearchInput?.Text?.Trim() ?? "";

            IEnumerable<GroupMembership> query = _groups;

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(item =>
                    item.Group.Name.Contains(
                        search,
                        StringComparison.CurrentCultureIgnoreCase)
                    ||
                    item.Role.Name.Contains(
                        search,
                        StringComparison.CurrentCultureIgnoreCase));
            }

            GroupMembership[] groups = query
                .OrderByDescending(item => item.Role.Rank >= 255)
                .ThenBy(item => item.Group.Name)
                .ToArray();

            GroupsWrapPanel.Children.Clear();

            foreach (GroupMembership membership in groups)
            {
                _groupIcons.TryGetValue(
                    membership.Group.Id,
                    out string? icon);

                GroupsWrapPanel.Children.Add(
                    CreateGroupCard(membership, icon));
            }

            GroupsStatusText.Text =
                groups.Length == 0
                    ? T(
                        "표시할 그룹이 없습니다.",
                        "No groups to display.")
                    : IsKorean
                        ? $"{groups.Length:N0}개 그룹 표시 중"
                        : $"Showing {groups.Length:N0} groups";
        }

        private Border CreateGroupCard(
            GroupMembership membership,
            string? iconUrl)
        {
            Border card = CreateBaseCard();
            StackPanel content = new StackPanel();

            content.Children.Add(
                CreateImageBox(iconUrl, false));

            content.Children.Add(
                CreateItemTitle(membership.Group.Name));

            if (membership.Role.Rank >= 255)
            {
                content.Children.Add(new TextBlock
                {
                    Text = T("소유자", "OWNER"),
                    Foreground = CreateBrush("#FFC857"),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(2, 6, 2, 0)
                });
            }

            content.Children.Add(new TextBlock
            {
                Text = IsKorean
                    ? $"역할 · {membership.Role.Name}"
                    : $"ROLE · {membership.Role.Name}",
                Foreground = CreateBrush("#B5BAC5"),
                FontSize = 11,
                Margin = new Thickness(2, 5, 2, 0)
            });

            content.Children.Add(new TextBlock
            {
                Text = IsKorean
                    ? $"랭크 · {membership.Role.Rank}"
                    : $"RANK · {membership.Role.Rank}",
                Foreground = CreateBrush("#777D89"),
                FontSize = 10,
                Margin = new Thickness(2, 3, 2, 0)
            });

            Button button = CreateCardButton(
                T("그룹 보기", "VIEW GROUP"));

            button.Click += (_, _) =>
                OpenWebPage(
                    $"https://www.roblox.com/communities/{membership.Group.Id}");

            content.Children.Add(button);
            card.Child = content;

            return card;
        }

        private async Task LoadInventoryAsync(bool reset)
        {
            if (_currentUserId == null || _isLoadingInventory)
                return;

            _isLoadingInventory = true;
            LoadMoreInventoryButton.IsEnabled = false;

            try
            {
                if (reset)
                {
                    _inventoryItems.Clear();
                    _inventoryThumbnails.Clear();
                    _inventoryCursor = null;
                    InventoryWrapPanel.Children.Clear();
                }

                InventoryStatusText.Text =
                    T(
                        "BETA 인벤토리를 불러오는 중...",
                        "Loading the BETA inventory...");

                await SafeDelayAsync();

                string url =
                    $"https://inventory.roblox.com/v2/users/{_currentUserId}/inventory" +
                    $"?assetTypes={InventoryAssetTypes}&limit=100&sortOrder=Desc";

                if (!string.IsNullOrWhiteSpace(_inventoryCursor))
                {
                    url +=
                        $"&cursor={Uri.EscapeDataString(_inventoryCursor)}";
                }

                InventoryResponse? response =
                    await Client.GetFromJsonAsync<InventoryResponse>(url);

                InventoryItem[] items =
                    response?.Data ?? Array.Empty<InventoryItem>();

                Dictionary<long, string> thumbnails =
                    await GetAssetThumbnailMapAsync(
                        items.Select(item => item.AssetId));

                foreach (InventoryItem item in items)
                {
                    if (!_inventoryItems.Any(
                        loaded => loaded.AssetId == item.AssetId))
                    {
                        item.Category = "OTHER";
                        _inventoryItems.Add(item);
                    }
                }

                foreach (var pair in thumbnails)
                    _inventoryThumbnails[pair.Key] = pair.Value;

                _inventoryCursor = response?.NextPageCursor;
                _loadedInventoryForUserId = _currentUserId;

                RenderInventoryItems();

                LoadMoreInventoryButton.Visibility =
                    string.IsNullOrWhiteSpace(_inventoryCursor)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
            }
            catch
            {
                InventoryStatusText.Text =
                    T(
                        "BETA · 현재 인벤토리 API가 불안정합니다.",
                        "BETA · The inventory API is currently unstable.");

                _loadedInventoryForUserId = null;
            }
            finally
            {
                _isLoadingInventory = false;
                LoadMoreInventoryButton.IsEnabled = true;
            }
        }

        private void RenderInventoryItems()
        {
            string sort =
                GetComboBoxTag(InventorySortComboBox, "Category");

            IEnumerable<InventoryItem> query = _inventoryItems;

            query = sort switch
            {
                "Newest" => query.OrderByDescending(item => item.Created),
                "Oldest" => query.OrderBy(item => item.Created),
                "NameAZ" => query.OrderBy(item => item.AssetName),
                "NameZA" => query.OrderByDescending(item => item.AssetName),
                _ => query.OrderBy(item => item.AssetName)
            };

            InventoryItem[] items = query.ToArray();
            InventoryWrapPanel.Children.Clear();

            foreach (InventoryItem item in items)
            {
                _inventoryThumbnails.TryGetValue(
                    item.AssetId,
                    out string? thumbnail);

                Border card = CreateBaseCard();
                StackPanel content = new StackPanel();

                content.Children.Add(
                    CreateImageBox(thumbnail, false));

                content.Children.Add(
                    CreateItemTitle(item.AssetName));

                Button button = CreateCardButton(
                    T("아이템 보기", "VIEW ITEM"));

                button.Click += (_, _) =>
                    OpenWebPage(
                        $"https://www.roblox.com/catalog/{item.AssetId}");

                content.Children.Add(button);
                card.Child = content;

                InventoryWrapPanel.Children.Add(card);
            }

            InventoryStatusText.Text =
                items.Length == 0
                    ? T(
                        "BETA · 표시할 아이템이 없습니다.",
                        "BETA · No items to display.")
                    : IsKorean
                        ? $"BETA · {items.Length:N0}개 표시 중"
                        : $"BETA · Showing {items.Length:N0} items";
        }

        private Border CreateBaseCard()
        {
            return new Border
            {
                Width = 188,
                MinHeight = 260,
                Background = CreateBrush("#131720"),
                BorderBrush = CreateBrush("#242A37"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(14),
                Margin = new Thickness(6),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 18,
                    ShadowDepth = 5,
                    Opacity = 0.22
                }
            };
        }

        private Border CreateImageBox(
            string? url,
            bool fill)
        {
            Border border = new Border
            {
                Width = 158,
                Height = 158,
                Background = CreateBrush("#202530"),
                BorderBrush = CreateBrush("#303746"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14)
            };

            Image image = new Image
            {
                Stretch = fill
                    ? Stretch.UniformToFill
                    : Stretch.Uniform
            };

            if (!string.IsNullOrWhiteSpace(url))
                image.Source = CreateBitmap(url, 150);

            border.Child = image;
            return border;
        }

        private TextBlock CreateItemTitle(string text)
        {
            return new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(text)
                    ? T("알 수 없음", "Unknown")
                    : text,

                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = CreateBrush("#F7F8FB"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(2, 11, 2, 0)
            };
        }

        private Button CreateCardButton(string text)
        {
            return new Button
            {
                Content = text,
                Height = 36,
                Margin = new Thickness(0, 12, 0, 0),
                Style =
                    (Style)FindResource("PrimaryButtonStyle")
            };
        }

        private async Task SafeDelayAsync(
            double multiplier = 1)
        {
            if (!_settings.SafeApiMode)
                return;

            int milliseconds =
                (int)(_settings.ApiDelayMs * multiplier);

            if (milliseconds > 0)
                await Task.Delay(milliseconds);
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFile))
                {
                    _settings = new AppSettings();
                    return;
                }

                string json =
                    File.ReadAllText(SettingsFile);

                _settings =
                    JsonSerializer.Deserialize<AppSettings>(json)
                    ?? new AppSettings();
            }
            catch
            {
                _settings = new AppSettings();
            }
        }

        private bool IsKorean =>
            !string.Equals(
                _settings.Language,
                "en",
                StringComparison.OrdinalIgnoreCase);

        private string T(
            string korean,
            string english)
        {
            return IsKorean ? korean : english;
        }

        private void ApplyLanguage()
        {
            MenuLabelText.Text = T("메뉴", "MENU");
            TrackerOnlineText.Text = T(
                "트래커 온라인",
                "TRACKER ONLINE");

            DashboardNavButton.Content = T(
                "▦    대시보드",
                "▦    Dashboard");

            SocialNavButton.Content = T(
                "♟    소셜",
                "♟    Social");

            InventoryNavButton.Content = T(
                "◇    인벤토리   · 베타",
                "◇    Inventory   · BETA");

            GroupsNavButton.Content = T(
                "◈    그룹",
                "◈    Groups");

            SettingsNavButton.Content = T(
                "⚙    설정",
                "⚙    Settings");

            UsernameInput.ToolTip = T(
                "Roblox 사용자명을 입력하세요",
                "Enter a Roblox username");

            SearchButton.Content = SearchButton.IsEnabled
                ? T("연결  →", "CONNECT  →")
                : T("불러오는 중...", "LOADING...");

            DashboardSectionText.Text = T(
                "계정 개요",
                "OVERVIEW");

            DashboardTitleText.Text = T(
                "계정 대시보드",
                "Account Dashboard");

            ProfileSourceText.Text = T(
                "ROBLOX 프로필",
                "ROBLOX PROFILE");

            LiveDataText.Text = T(
                "실시간 데이터",
                "LIVE DATA");

            UserIdLabelText.Text = T("사용자 ID", "USER ID");
            CreatedLabelText.Text = T("가입일", "CREATED");
            AccountAgeLabelText.Text = T("계정 나이", "ACCOUNT AGE");
            FriendsLabelText.Text = T("친구", "FRIENDS");
            FollowersLabelText.Text = T("팔로워", "FOLLOWERS");
            FollowingLabelText.Text = T("팔로잉", "FOLLOWING");

            SocialSectionText.Text = T(
                "소셜 네트워크",
                "SOCIAL NETWORK");

            PublicConnectionsText.Text = T(
                "공개 연결 정보",
                "PUBLIC CONNECTIONS");

            SocialFriendsLabelText.Text = T("친구", "FRIENDS");
            SocialFollowersLabelText.Text = T("팔로워", "FOLLOWERS");
            SocialFollowingLabelText.Text = T("팔로잉", "FOLLOWING");
            FriendsHeadingText.Text = T("친구", "FRIENDS");
            FriendsSubtitleText.Text = T(
                "Roblox 친구 목록",
                "Roblox friends list");

            InventorySectionText.Text = T("인벤토리", "INVENTORY");
            InventorySortLabelText.Text = T("정렬", "SORT");
            InventorySortHintText.Text = T("정렬 기준", "ORDER");
            InventoryTypeLabelText.Text = T("종류", "TYPE");
            InventoryTypeHintText.Text = T("카테고리", "CATEGORY");

            GroupsSectionText.Text = T("커뮤니티", "COMMUNITIES");
            GroupSearchInput.ToolTip = T(
                "그룹 이름 검색",
                "Search group names");

            SettingsSectionText.Text = T("환경 설정", "PREFERENCES");
            SettingsTitleText.Text = T("앱 설정", "Application Settings");
            LocalSettingsText.Text = T("로컬 설정", "LOCAL SETTINGS");

            AccountSectionText.Text = T("계정", "ACCOUNT");
            AccountSettingsTitleText.Text = T(
                "계정 연결 설정",
                "Account connection");

            AccountSettingsDescriptionText.Text = T(
                "앱을 다시 실행했을 때 사용할 계정 연결 방식을 정합니다.",
                "Choose how the account is connected when the app starts.");

            RememberUsernameCheckBox.Content = T(
                "마지막 사용자명 기억",
                "Remember last username");

            AutoConnectCheckBox.Content = T(
                "앱 실행 시 자동 연결",
                "Connect automatically on startup");

            ApiSectionText.Text = "ROBLOX API";
            ApiSettingsTitleText.Text = T(
                "요청 안정성",
                "Request stability");

            SafeApiModeCheckBox.Content = T(
                "API 안전 모드 사용 (추천)",
                "Use safe API mode (recommended)");

            ApiDelayLabelText.Text = T(
                "요청 간격",
                "Request interval");

            ApiDelayDescriptionText.Text = T(
                "간격이 길수록 429 오류가 줄어듭니다.",
                "A longer interval helps reduce 429 errors.");

            LanguageSectionText.Text = T("언어", "LANGUAGE");
            LanguageTitleText.Text = T("언어", "Language");
            LanguageDescriptionText.Text = T(
                "앱에서 사용할 언어를 선택합니다.",
                "Choose the language used by the app.");

            StartupSectionText.Text = T("시작 화면", "STARTUP");
            StartupTitleText.Text = T("기본 화면", "Default screen");
            StartupDescriptionText.Text = T(
                "계정 연결 후 처음 표시할 탭을 선택합니다.",
                "Choose the first tab shown after connecting.");

            SaveSettingsButton.Content = T(
                "설정 저장  ✓",
                "SAVE SETTINGS  ✓");

            ResetSettingsButton.Content = T("초기화", "RESET");
            LoadMoreInventoryButton.Content = T(
                "더 불러오기  ↓",
                "LOAD MORE  ↓");

            SetComboBoxItemContent(
                InventorySortComboBox,
                "Category",
                T("카테고리", "CATEGORY"));

            SetComboBoxItemContent(
                InventorySortComboBox,
                "Newest",
                T("최신순", "NEWEST"));

            SetComboBoxItemContent(
                InventorySortComboBox,
                "Oldest",
                T("오래된순", "OLDEST"));

            SetComboBoxItemContent(
                InventorySortComboBox,
                "NameAZ",
                T("이름 A-Z", "NAME A-Z"));

            SetComboBoxItemContent(
                InventorySortComboBox,
                "NameZA",
                T("이름 Z-A", "NAME Z-A"));

            SetComboBoxItemContent(
                InventoryCategoryComboBox,
                "ALL",
                T("전체", "ALL"));

            SetComboBoxItemContent(
                InventoryCategoryComboBox,
                "ACCESSORIES",
                T("액세서리", "ACCESSORIES"));

            SetComboBoxItemContent(
                InventoryCategoryComboBox,
                "CLOTHING",
                T("의류", "CLOTHING"));

            SetComboBoxItemContent(
                InventoryCategoryComboBox,
                "HEADS & FACES",
                T("머리 & 얼굴", "HEADS & FACES"));

            SetComboBoxItemContent(
                InventoryCategoryComboBox,
                "ANIMATIONS",
                T("애니메이션", "ANIMATIONS"));

            SetComboBoxItemContent(
                InventoryCategoryComboBox,
                "GEAR",
                T("장비", "GEAR"));

            SetComboBoxItemContent(
                InventoryCategoryComboBox,
                "OTHER",
                T("기타", "OTHER"));

            SetComboBoxItemContent(
                DefaultTabComboBox,
                "Dashboard",
                T("대시보드", "Dashboard"));

            SetComboBoxItemContent(
                DefaultTabComboBox,
                "Social",
                T("소셜", "Social"));

            SetComboBoxItemContent(
                DefaultTabComboBox,
                "Groups",
                T("그룹", "Groups"));

            SetComboBoxItemContent(
                DefaultTabComboBox,
                "Inventory",
                T("인벤토리 (베타)", "Inventory (Beta)"));

            ApiDelayValueText.Text = IsKorean
                ? $"{ApiDelaySlider.Value / 1000:0.0}초"
                : $"{ApiDelaySlider.Value / 1000:0.0}s";

            if (_currentUser != null)
            {
                UserDetails user = _currentUser;

                CreatedText.Text = FormatCreatedDate(user.Created);
                AccountAgeText.Text = GetAccountAge(user.Created);
                UpdateUserSectionTitles(user.DisplayName);

                DescriptionText.Text =
                    string.IsNullOrWhiteSpace(user.Description)
                        ? T("소개글이 없습니다.", "No description.")
                        : user.Description;

                AccountStatusText.Text = user.IsBanned
                    ? T("이용 정지", "BANNED")
                    : T("활성", "ACTIVE");

                StatusText.Text = IsKorean
                    ? $"{user.DisplayName} 계정을 연결했습니다."
                    : $"Connected to {user.DisplayName}.";
            }
            else
            {
                StatusText.Text = T(
                    "Roblox 사용자명을 입력하세요.",
                    "Enter a Roblox username.");

                DisplayNameText.Text = T("표시 이름", "Display Name");
                DescriptionText.Text = T(
                    "사용자를 검색하면 소개글이 표시됩니다.",
                    "Search for a user to see their description.");

                AccountStatusText.Text = T(
                    "연결되지 않음",
                    "NOT CONNECTED");

                SocialTitleText.Text = T("소셜", "Social");
                InventoryTitleText.Text = T("인벤토리", "Inventory");
                GroupsTitleText.Text = T("그룹", "Groups");
            }

            if (_currentUserId != null &&
                _loadedFriendsForUserId == _currentUserId)
            {
                UpdateCardButtonTexts(
                    FriendsWrapPanel,
                    T("친구 추가", "ADD FRIEND"));

                FriendsListStatusText.Text = IsKorean
                    ? $"{FriendsWrapPanel.Children.Count:N0}명의 친구"
                    : $"{FriendsWrapPanel.Children.Count:N0} friends";
            }
            else
            {
                FriendsListStatusText.Text = T(
                    "친구 목록을 불러옵니다.",
                    "Friends will appear here.");
            }

            if (_currentUserId != null &&
                _loadedGroupsForUserId == _currentUserId)
            {
                RenderGroups();
            }
            else
            {
                GroupsStatusText.Text = T(
                    "계정을 연결한 뒤 Groups를 열어주세요.",
                    "Connect an account, then open Groups.");
            }

            if (_currentUserId != null &&
                _loadedInventoryForUserId == _currentUserId)
            {
                RenderInventoryItems();
            }
            else
            {
                InventoryStatusText.Text = T(
                    "BETA 기능입니다.",
                    "This feature is in BETA.");
            }
        }

        private void UpdateUserSectionTitles(string displayName)
        {
            SocialTitleText.Text = IsKorean
                ? $"{displayName}님의 소셜"
                : $"{displayName}'s Social";

            InventoryTitleText.Text = IsKorean
                ? $"{displayName}님의 인벤토리"
                : $"{displayName}'s Inventory";

            GroupsTitleText.Text = IsKorean
                ? $"{displayName}님의 그룹"
                : $"{displayName}'s Groups";
        }

        private string FormatCreatedDate(DateTimeOffset created)
        {
            DateTimeOffset local = created.ToLocalTime();

            return IsKorean
                ? local.ToString("yyyy년 M월 d일")
                : local.ToString(
                    "MMM d, yyyy",
                    CultureInfo.InvariantCulture);
        }

        private static void SetComboBoxItemContent(
            ComboBox comboBox,
            string tag,
            string content)
        {
            foreach (object entry in comboBox.Items)
            {
                if (entry is ComboBoxItem item &&
                    item.Tag?.ToString() == tag)
                {
                    item.Content = content;
                    return;
                }
            }
        }

        private static void UpdateCardButtonTexts(
            WrapPanel panel,
            string text)
        {
            foreach (Border card in panel.Children.OfType<Border>())
            {
                if (card.Child is not StackPanel content)
                    continue;

                foreach (Button button in content.Children.OfType<Button>())
                    button.Content = text;
            }
        }

        private void ApplySettingsToControls()
        {
            _isApplyingSettings = true;

            try
            {
                RememberUsernameCheckBox.IsChecked =
                    _settings.RememberUsername;

                AutoConnectCheckBox.IsChecked =
                    _settings.AutoConnect;

                SafeApiModeCheckBox.IsChecked =
                    _settings.SafeApiMode;

                ApiDelaySlider.Value =
                    Math.Clamp(_settings.ApiDelayMs, 500, 5000);

                AutoConnectCheckBox.IsEnabled =
                    _settings.RememberUsername;

                if (_settings.RememberUsername)
                    UsernameInput.Text = _settings.LastUsername;

                SelectComboBoxTag(
                    DefaultTabComboBox,
                    _settings.DefaultTab);

                SelectComboBoxTag(
                    LanguageComboBox,
                    _settings.Language);
            }
            finally
            {
                _isApplyingSettings = false;
            }

            ApplyLanguage();
        }

        private void ReadSettingsFromControls()
        {
            _settings.RememberUsername =
                RememberUsernameCheckBox.IsChecked == true;

            _settings.AutoConnect =
                _settings.RememberUsername &&
                AutoConnectCheckBox.IsChecked == true;

            _settings.SafeApiMode =
                SafeApiModeCheckBox.IsChecked == true;

            _settings.ApiDelayMs =
                ApiDelaySlider.Value;

            _settings.DefaultTab =
                GetComboBoxTag(
                    DefaultTabComboBox,
                    "Dashboard");

            _settings.Language =
                GetComboBoxTag(
                    LanguageComboBox,
                    "ko");
        }

        private bool WriteSettings()
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);

                string json = JsonSerializer.Serialize(
                    _settings,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(SettingsFile, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SelectComboBoxTag(
            ComboBox comboBox,
            string value)
        {
            foreach (object entry in comboBox.Items)
            {
                if (entry is ComboBoxItem item &&
                    item.Tag?.ToString() == value)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }

            comboBox.SelectedIndex = 0;
        }

        private static string GetComboBoxTag(
            ComboBox comboBox,
            string fallback)
        {
            return comboBox.SelectedItem is ComboBoxItem item
                ? item.Tag?.ToString() ?? fallback
                : fallback;
        }

        private static async Task<Dictionary<long, FriendUser>>
            GetUserProfilesAsync(IEnumerable<long> userIds)
        {
            Dictionary<long, FriendUser> result = new();
            long[] ids = userIds.Distinct().ToArray();

            for (int start = 0; start < ids.Length; start += 100)
            {
                long[] batch =
                    ids.Skip(start).Take(100).ToArray();

                try
                {
                    HttpResponseMessage response =
                        await Client.PostAsJsonAsync(
                            "https://users.roblox.com/v1/users",
                            new
                            {
                                userIds = batch,
                                excludeBannedUsers = false
                            });

                    UsersResponse? users =
                        await response.Content
                            .ReadFromJsonAsync<UsersResponse>();

                    if (users == null)
                        continue;

                    foreach (FriendUser user in users.Data)
                        result[user.Id] = user;
                }
                catch
                {
                }
            }

            return result;
        }

        private static Task<Dictionary<long, string>>
            GetAvatarMapAsync(IEnumerable<long> ids)
        {
            return GetThumbnailMapAsync(
                ids,
                "https://thumbnails.roblox.com/v1/users/avatar-headshot" +
                "?userIds={0}&size=150x150&format=Png&isCircular=false");
        }

        private static Task<Dictionary<long, string>>
            GetGroupIconMapAsync(IEnumerable<long> ids)
        {
            return GetThumbnailMapAsync(
                ids,
                "https://thumbnails.roblox.com/v1/groups/icons" +
                "?groupIds={0}&size=150x150&format=Png&isCircular=false");
        }

        private static Task<Dictionary<long, string>>
            GetAssetThumbnailMapAsync(IEnumerable<long> ids)
        {
            return GetThumbnailMapAsync(
                ids,
                "https://thumbnails.roblox.com/v1/assets" +
                "?assetIds={0}&returnPolicy=PlaceHolder" +
                "&size=150x150&format=Png&isCircular=false");
        }

        private static async Task<Dictionary<long, string>>
            GetThumbnailMapAsync(
                IEnumerable<long> idSource,
                string urlFormat)
        {
            Dictionary<long, string> result = new();
            long[] ids = idSource.Distinct().ToArray();

            for (int start = 0; start < ids.Length; start += 100)
            {
                string idText = string.Join(
                    ",",
                    ids.Skip(start).Take(100));

                ThumbnailResponse? response =
                    await Client.GetFromJsonAsync<ThumbnailResponse>(
                        string.Format(urlFormat, idText));

                if (response == null)
                    continue;

                foreach (ThumbnailItem item in response.Data)
                {
                    if (!string.IsNullOrWhiteSpace(item.ImageUrl))
                        result[item.TargetId] = item.ImageUrl;
                }
            }

            return result;
        }

        private static async Task<string> GetCountAsync(
            string url)
        {
            try
            {
                CountResponse? response =
                    await Client.GetFromJsonAsync<CountResponse>(url);

                return response?.Count.ToString("N0") ?? "—";
            }
            catch
            {
                return "—";
            }
        }

        private static async Task<string?> GetAvatarUrlAsync(
            long userId)
        {
            try
            {
                ThumbnailResponse? response =
                    await Client.GetFromJsonAsync<ThumbnailResponse>(
                        "https://thumbnails.roblox.com/v1/users/avatar" +
                        $"?userIds={userId}&size=420x420" +
                        "&format=Png&isCircular=false");

                return response?.Data.FirstOrDefault()?.ImageUrl;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapImage CreateBitmap(
            string url,
            int width)
        {
            BitmapImage bitmap = new BitmapImage();

            bitmap.BeginInit();
            bitmap.UriSource = new Uri(url);
            bitmap.DecodePixelWidth = width;
            bitmap.CacheOption = BitmapCacheOption.OnDemand;
            bitmap.CreateOptions =
                BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();

            return bitmap;
        }

        private static bool IsTooManyRequests(
            HttpRequestException? ex)
        {
            if (ex == null)
                return false;

            return
                (ex.StatusCode.HasValue &&
                 (int)ex.StatusCode.Value == 429)
                ||
                ex.Message.Contains("429");
        }

        private static void OpenWebPage(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        private string GetAccountAge(
            DateTimeOffset created)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            int years = now.Year - created.Year;

            if (created.AddYears(years) > now)
                years--;

            return IsKorean
                ? $"{years}년 · {(now - created).Days:N0}일"
                : $"{years}y · {(now - created).Days:N0}d";
        }

        private void SetLoading(bool loading)
        {
            SearchButton.IsEnabled = !loading;
            SearchButton.Content =
                loading
                    ? T("불러오는 중...", "LOADING...")
                    : T("연결  →", "CONNECT  →");

            if (loading)
            {
                StatusText.Text = T(
                    "계정 정보를 불러오는 중...",
                    "Loading account information...");
            }
        }

        private void ResetProfile()
        {
            _currentUserId = null;
            _currentUser = null;
            _loadedFriendsForUserId = null;
            _loadedInventoryForUserId = null;
            _loadedGroupsForUserId = null;
            _inventoryCursor = null;

            _inventoryItems.Clear();
            _inventoryThumbnails.Clear();
            _groups.Clear();
            _groupIcons.Clear();

            DisplayNameText.Text = T("표시 이름", "Display Name");
            UsernameText.Text = "@username";
            UserIdText.Text = "—";
            CreatedText.Text = "—";
            AccountAgeText.Text = "—";
            FriendsText.Text = "—";
            FollowersText.Text = "—";
            FollowingText.Text = "—";

            SocialFriendsText.Text = "—";
            SocialFollowersText.Text = "—";
            SocialFollowingText.Text = "—";

            SocialTitleText.Text = T("소셜", "Social");
            InventoryTitleText.Text = T("인벤토리", "Inventory");
            GroupsTitleText.Text = T("그룹", "Groups");

            DescriptionText.Text = T(
                "사용자를 검색하면 소개글이 표시됩니다.",
                "Search for a user to see their description.");

            AvatarImage.Source = null;

            FriendsWrapPanel.Children.Clear();
            InventoryWrapPanel.Children.Clear();
            GroupsWrapPanel.Children.Clear();

            VerifiedBadge.Visibility = Visibility.Collapsed;
            LoadMoreInventoryButton.Visibility = Visibility.Collapsed;

            AccountStatusText.Text = T(
                "연결되지 않음",
                "NOT CONNECTED");

            AccountStatusText.Foreground =
                CreateBrush("#55D6A0");

            AccountStatusBorder.Background =
                CreateBrush("#193A2D");

            ShowDashboard();
        }

        private static SolidColorBrush CreateBrush(string color)
        {
            return new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(color));
        }

        private sealed class AppSettings
        {
            public bool RememberUsername { get; set; } = true;
            public bool AutoConnect { get; set; }
            public bool SafeApiMode { get; set; } = true;
            public double ApiDelayMs { get; set; } = 1500;
            public string DefaultTab { get; set; } = "Dashboard";
            public string Language { get; set; } = "ko";
            public string LastUsername { get; set; } = "";
        }

        private sealed class UsernameLookupResponse
        {
            public LookupUser[] Data { get; set; }
                = Array.Empty<LookupUser>();
        }

        private sealed class LookupUser
        {
            public long Id { get; set; }
            public string Name { get; set; } = "";
            public string DisplayName { get; set; } = "";
        }

        private sealed class UserDetails
        {
            public long Id { get; set; }
            public string Name { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string Description { get; set; } = "";
            public DateTimeOffset Created { get; set; }
            public bool IsBanned { get; set; }
            public bool HasVerifiedBadge { get; set; }
        }

        private sealed class FriendsResponse
        {
            public FriendUser[] Data { get; set; }
                = Array.Empty<FriendUser>();
        }

        private sealed class UsersResponse
        {
            public FriendUser[] Data { get; set; }
                = Array.Empty<FriendUser>();
        }

        private sealed class FriendUser
        {
            public long Id { get; set; }
            public string Name { get; set; } = "";
            public string DisplayName { get; set; } = "";
        }

        private sealed class GroupsResponse
        {
            public GroupMembership[] Data { get; set; }
                = Array.Empty<GroupMembership>();
        }

        private sealed class GroupMembership
        {
            public GroupInfo Group { get; set; } = new();
            public GroupRole Role { get; set; } = new();
        }

        private sealed class GroupInfo
        {
            public long Id { get; set; }
            public string Name { get; set; } = "";
        }

        private sealed class GroupRole
        {
            public string Name { get; set; } = "";
            public int Rank { get; set; }
        }

        private sealed class InventoryResponse
        {
            public string? NextPageCursor { get; set; }

            public InventoryItem[] Data { get; set; }
                = Array.Empty<InventoryItem>();
        }

        private sealed class InventoryItem
        {
            public long AssetId { get; set; }
            public string AssetName { get; set; } = "";
            public DateTimeOffset Created { get; set; }
            public string Category { get; set; } = "OTHER";
        }

        private sealed class CountResponse
        {
            public int Count { get; set; }
        }

        private sealed class ThumbnailResponse
        {
            public ThumbnailItem[] Data { get; set; }
                = Array.Empty<ThumbnailItem>();
        }

        private sealed class ThumbnailItem
        {
            public long TargetId { get; set; }
            public string ImageUrl { get; set; } = "";
        }
    }
}
