using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PixelSaur.UI
{
    [Serializable]
    public class FriendEntryData
    {
        public string playerId;
        public string nickname;
        public int level;
        public string dinoTag;
        public bool isOnline;
        public string statusDesc;
        public float accentR;
        public float accentG;
        public float accentB;

        public Color GetAccentColor()
        {
            return new Color(accentR, accentG, accentB, 1f);
        }
    }

    [Serializable]
    public class FriendRequestData
    {
        public string playerId;
        public string nickname;
        public int level;
        public string dinoTag;
        public bool isOnline;
        public string requestNote;
        public float accentR;
        public float accentG;
        public float accentB;

        public Color GetAccentColor()
        {
            return new Color(accentR, accentG, accentB, 1f);
        }
    }

    [Serializable]
    internal class FriendSaveData
    {
        public List<FriendEntryData> friends = new List<FriendEntryData>();
        public List<FriendRequestData> requests = new List<FriendRequestData>();
    }

    /// <summary>
    /// 本地模拟好友与查找玩家管理器（纯本地 JSON + PlayerPrefs 持久化，不含联机服务器长连接）：
    /// 1. 维护本地好友列表（含在线/离线状态占位）、待处理好友申请列表；
    /// 2. 支持按玩家 ID 或昵称搜索本地模拟玩家库（含任意新 ID 动态模拟生成）；
    /// 3. 支持增删好友及申请同意/拒绝操作并即时保存至本地存档。
    /// </summary>
    public static class FriendManager
    {
        public const string SelfPlayerId = "10001";
        public const string SelfNickname = "驯龙者 #001";

        private const string SaveFileName = "pixelsaur_friends_v1.json";
        private const string PrefsBackupKey = "PIXELSaur_FRIENDS_JSON_V1";

        public static event Action OnFriendsChanged;

        private static bool _initialized;
        private static readonly List<FriendEntryData> Friends = new List<FriendEntryData>();
        private static readonly List<FriendRequestData> Requests = new List<FriendRequestData>();
        private static readonly List<FriendEntryData> MockDirectory = new List<FriendEntryData>();

        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            BuildMockDirectory();
            LoadFromDisk();
        }

        private static void BuildMockDirectory()
        {
            MockDirectory.Clear();

            AddMockPlayer("10005", "火山小霸王", 14, "成年霸王龙", true, "在线 · 岛屿海岸线巡逻", new Color(0.95f, 0.42f, 0.24f, 1f));
            AddMockPlayer("10006", "冰原翼龙王", 19, "苍穹翼龙", true, "在线 · 等待组队探索", new Color(0.26f, 0.76f, 0.98f, 1f));
            AddMockPlayer("10007", "黄金琥珀猎手", 22, "狂暴领主霸王龙", false, "离线 · 35分钟前", new Color(0.98f, 0.76f, 0.24f, 1f));
            AddMockPlayer("10008", "夜幕潜行者", 11, "迅捷亚成体霸王龙", true, "在线 · 密林草丛潜行中", new Color(0.32f, 0.84f, 0.56f, 1f));
            AddMockPlayer("202601", "方块像素君", 16, "浅海棘背龙", false, "离线 · 昨日", new Color(0.56f, 0.62f, 0.98f, 1f));
            AddMockPlayer("202602", "林深见恐龙", 27, "黑曜炎狱霸王龙", true, "在线 · 像素岛屿大厅", new Color(0.72f, 0.46f, 0.98f, 1f));
        }

        private static void AddMockPlayer(string id, string nick, int lv, string dino, bool online, string status, Color accent)
        {
            MockDirectory.Add(new FriendEntryData
            {
                playerId = id,
                nickname = nick,
                level = lv,
                dinoTag = dino,
                isOnline = online,
                statusDesc = status,
                accentR = accent.r,
                accentG = accent.g,
                accentB = accent.b
            });
        }

        private static void SeedDefaults()
        {
            Friends.Clear();
            Requests.Clear();

            Friends.Add(new FriendEntryData
            {
                playerId = "10002",
                nickname = "暴龙小队长",
                level = 12,
                dinoTag = "成年霸王龙",
                isOnline = true,
                statusDesc = "在线 · 岛屿大厅",
                accentR = 0.24f, accentG = 0.78f, accentB = 0.42f
            });
            Friends.Add(new FriendEntryData
            {
                playerId = "10003",
                nickname = "沧海棘背龙",
                level = 18,
                dinoTag = "浅海棘背龙",
                isOnline = true,
                statusDesc = "在线 · 浅海觅食中",
                accentR = 0.22f, accentG = 0.72f, accentB = 0.96f
            });
            Friends.Add(new FriendEntryData
            {
                playerId = "10004",
                nickname = "赤焰战纹师",
                level = 9,
                dinoTag = "赤焰火山霸王龙",
                isOnline = true,
                statusDesc = "在线 · 空闲可组队",
                accentR = 0.96f, accentG = 0.46f, accentB = 0.24f
            });
            Friends.Add(new FriendEntryData
            {
                playerId = "10009",
                nickname = "星夜三角龙",
                level = 7,
                dinoTag = "迅捷亚成体",
                isOnline = false,
                statusDesc = "离线 · 2小时前",
                accentR = 0.56f, accentG = 0.62f, accentB = 0.72f
            });
            Friends.Add(new FriendEntryData
            {
                playerId = "10010",
                nickname = "远古守望者",
                level = 25,
                dinoTag = "狂暴领主霸王龙",
                isOnline = false,
                statusDesc = "离线 · 3天前",
                accentR = 0.68f, accentG = 0.52f, accentB = 0.88f
            });

            Requests.Add(new FriendRequestData
            {
                playerId = "10011",
                nickname = "岛屿探索喵",
                level = 8,
                dinoTag = "迅捷亚成体霸王龙",
                isOnline = true,
                requestNote = "你好！一起去海边草丛狩猎吗？",
                accentR = 0.34f, accentG = 0.86f, accentB = 0.62f
            });
            Requests.Add(new FriendRequestData
            {
                playerId = "10012",
                nickname = "像素老猎手",
                level = 21,
                dinoTag = "狂暴领主霸王龙",
                isOnline = true,
                requestNote = "看到你的恐龙背包皮肤很酷，加个好友~",
                accentR = 0.96f, accentG = 0.70f, accentB = 0.24f
            });
            Requests.Add(new FriendRequestData
            {
                playerId = "10013",
                nickname = "碧海小飞龙",
                level = 6,
                dinoTag = "苍穹翼龙",
                isOnline = false,
                requestNote = "刚登岛的萌新，请求互加好友！",
                accentR = 0.32f, accentG = 0.74f, accentB = 0.98f
            });
        }

        private static void LoadFromDisk()
        {
            string json = null;
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    json = File.ReadAllText(SaveFilePath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FriendManager] 读取本地好友 JSON 文件失败，尝试 PlayerPrefs 回退：{e.Message}");
            }

            if (string.IsNullOrEmpty(json) && PlayerPrefs.HasKey(PrefsBackupKey))
            {
                json = PlayerPrefs.GetString(PrefsBackupKey, string.Empty);
            }

            if (string.IsNullOrEmpty(json))
            {
                SeedDefaults();
                SaveToDisk();
                return;
            }

            try
            {
                FriendSaveData save = JsonUtility.FromJson<FriendSaveData>(json);
                if (save == null)
                {
                    SeedDefaults();
                    SaveToDisk();
                    return;
                }

                Friends.Clear();
                if (save.friends != null) Friends.AddRange(save.friends);
                Requests.Clear();
                if (save.requests != null) Requests.AddRange(save.requests);
                SortFriendsList();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FriendManager] 解析好友存档失败，将重置为默认列表：{e.Message}");
                SeedDefaults();
                SaveToDisk();
            }
        }

        public static void SaveToDisk()
        {
            EnsureInitialized();
            SortFriendsList();

            var save = new FriendSaveData
            {
                friends = new List<FriendEntryData>(Friends),
                requests = new List<FriendRequestData>(Requests)
            };

            string json = JsonUtility.ToJson(save, true);
            PlayerPrefs.SetString(PrefsBackupKey, json);
            PlayerPrefs.Save();

            try
            {
                string dir = Path.GetDirectoryName(SaveFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(SaveFilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FriendManager] 写入本地好友 JSON 失败（已写入 PlayerPrefs）：{e.Message}");
            }
        }

        private static void SortFriendsList()
        {
            Friends.Sort((a, b) =>
            {
                int onlineCmp = b.isOnline.CompareTo(a.isOnline);
                if (onlineCmp != 0) return onlineCmp;
                return string.Compare(a.playerId, b.playerId, StringComparison.Ordinal);
            });
        }

        public static List<FriendEntryData> GetFriends()
        {
            EnsureInitialized();
            return new List<FriendEntryData>(Friends);
        }

        public static int GetOnlineFriendCount()
        {
            EnsureInitialized();
            int count = 0;
            for (int i = 0; i < Friends.Count; i++)
            {
                if (Friends[i].isOnline) count++;
            }
            return count;
        }

        public static List<FriendRequestData> GetRequests()
        {
            EnsureInitialized();
            return new List<FriendRequestData>(Requests);
        }

        public static int GetPendingRequestCount()
        {
            EnsureInitialized();
            return Requests.Count;
        }

        public static bool IsFriend(string playerId)
        {
            EnsureInitialized();
            for (int i = 0; i < Friends.Count; i++)
            {
                if (Friends[i].playerId == playerId) return true;
            }
            return false;
        }

        public static bool HasPendingRequest(string playerId)
        {
            EnsureInitialized();
            for (int i = 0; i < Requests.Count; i++)
            {
                if (Requests[i].playerId == playerId) return true;
            }
            return false;
        }

        public static List<FriendEntryData> SearchPlayers(string rawQuery, out string hintMessage)
        {
            EnsureInitialized();
            string query = rawQuery != null ? rawQuery.Trim() : string.Empty;
            var results = new List<FriendEntryData>();

            if (string.IsNullOrEmpty(query))
            {
                hintMessage = "推荐玩家（也可输入 ID 精准查找，如 10005 或任意 ID）";
                for (int i = 0; i < MockDirectory.Count; i++)
                {
                    if (!IsFriend(MockDirectory[i].playerId))
                    {
                        results.Add(MockDirectory[i]);
                    }
                }
                if (results.Count == 0)
                {
                    results.AddRange(MockDirectory);
                }
                return results;
            }

            if (query == SelfPlayerId || query == SelfNickname)
            {
                hintMessage = "提示：这是你自己的玩家 ID（10001）";
                return results;
            }

            // 1. 先查好友与预设模拟玩家目录
            for (int i = 0; i < Friends.Count; i++)
            {
                if (MatchesQuery(Friends[i], query)) results.Add(Friends[i]);
            }

            for (int i = 0; i < MockDirectory.Count; i++)
            {
                FriendEntryData mock = MockDirectory[i];
                if (MatchesQuery(mock, query) && !ContainsId(results, mock.playerId))
                {
                    results.Add(mock);
                }
            }

            // 2. 再查待处理申请中是否有对应玩家
            for (int i = 0; i < Requests.Count; i++)
            {
                FriendRequestData req = Requests[i];
                if ((req.playerId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     req.nickname.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) &&
                    !ContainsId(results, req.playerId))
                {
                    results.Add(new FriendEntryData
                    {
                        playerId = req.playerId,
                        nickname = req.nickname,
                        level = req.level,
                        dinoTag = req.dinoTag,
                        isOnline = req.isOnline,
                        statusDesc = req.isOnline ? "在线 · 已向你发送申请" : "离线 · 已向你发送申请",
                        accentR = req.accentR,
                        accentG = req.accentG,
                        accentB = req.accentB
                    });
                }
            }

            // 3. 若未命中预设目录但输入了有效 ID，本地生成模拟玩家供测试添加闭环
            if (results.Count == 0 && query.Length >= 2)
            {
                int hash = Mathf.Abs(query.GetHashCode());
                bool online = hash % 3 != 0;
                int lv = 5 + (hash % 24);
                string[] dinos = { "成年霸王龙", "迅捷亚成体", "苍穹翼龙", "浅海棘背龙", "黑曜霸王龙" };
                Color[] accents =
                {
                    new Color(0.28f, 0.80f, 0.46f, 1f),
                    new Color(0.26f, 0.74f, 0.98f, 1f),
                    new Color(0.96f, 0.64f, 0.24f, 1f),
                    new Color(0.72f, 0.46f, 0.98f, 1f)
                };
                Color c = accents[hash % accents.Length];
                results.Add(new FriendEntryData
                {
                    playerId = query,
                    nickname = $"岛屿玩家 #{query}",
                    level = lv,
                    dinoTag = dinos[hash % dinos.Length],
                    isOnline = online,
                    statusDesc = online ? "在线 · 探索中" : "离线 · 1小时前",
                    accentR = c.r,
                    accentG = c.g,
                    accentB = c.b
                });
                hintMessage = $"已匹配本地模拟玩家 ID：{query}（可直接加为好友）";
                return results;
            }

            hintMessage = results.Count > 0
                ? $"共找到 {results.Count} 名匹配玩家"
                : $"未找到与“{query}”匹配的玩家（请输入至少 2 位 ID）";
            return results;
        }

        private static bool MatchesQuery(FriendEntryData entry, string query)
        {
            return entry.playerId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   entry.nickname.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsId(List<FriendEntryData> list, string playerId)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].playerId == playerId) return true;
            }
            return false;
        }

        public static bool AddFriend(FriendEntryData candidate, out string feedbackMessage)
        {
            EnsureInitialized();
            if (candidate == null || string.IsNullOrEmpty(candidate.playerId))
            {
                feedbackMessage = "无效的玩家信息。";
                return false;
            }
            if (candidate.playerId == SelfPlayerId)
            {
                feedbackMessage = "不能添加自己为好友。";
                return false;
            }
            if (IsFriend(candidate.playerId))
            {
                feedbackMessage = $"【{candidate.nickname}】已在好友列表中。";
                return false;
            }

            // 如果原本在申请列表里，直接转为通过申请
            for (int i = Requests.Count - 1; i >= 0; i--)
            {
                if (Requests[i].playerId == candidate.playerId)
                {
                    Requests.RemoveAt(i);
                }
            }

            Friends.Add(new FriendEntryData
            {
                playerId = candidate.playerId,
                nickname = candidate.nickname,
                level = candidate.level,
                dinoTag = candidate.dinoTag,
                isOnline = candidate.isOnline,
                statusDesc = candidate.isOnline ? "在线 · 刚成为好友" : "离线 · 刚成为好友",
                accentR = candidate.accentR,
                accentG = candidate.accentG,
                accentB = candidate.accentB
            });

            SaveToDisk();
            OnFriendsChanged?.Invoke();
            feedbackMessage = $"✓ 已将【{candidate.nickname}】(ID:{candidate.playerId}) 添加至好友列表！";
            return true;
        }

        public static bool RemoveFriend(string playerId, out string feedbackMessage)
        {
            EnsureInitialized();
            for (int i = 0; i < Friends.Count; i++)
            {
                if (Friends[i].playerId == playerId)
                {
                    string removedName = Friends[i].nickname;
                    Friends.RemoveAt(i);
                    SaveToDisk();
                    OnFriendsChanged?.Invoke();
                    feedbackMessage = $"✓ 已删除好友【{removedName}】(ID:{playerId})";
                    return true;
                }
            }
            feedbackMessage = "未找到该好友。";
            return false;
        }

        public static bool AcceptRequest(string playerId, out string feedbackMessage)
        {
            EnsureInitialized();
            for (int i = 0; i < Requests.Count; i++)
            {
                FriendRequestData req = Requests[i];
                if (req.playerId == playerId)
                {
                    Requests.RemoveAt(i);
                    if (!IsFriend(req.playerId))
                    {
                        Friends.Add(new FriendEntryData
                        {
                            playerId = req.playerId,
                            nickname = req.nickname,
                            level = req.level,
                            dinoTag = req.dinoTag,
                            isOnline = req.isOnline,
                            statusDesc = req.isOnline ? "在线 · 刚刚通过申请" : "离线 · 刚刚通过申请",
                            accentR = req.accentR,
                            accentG = req.accentG,
                            accentB = req.accentB
                        });
                    }
                    SaveToDisk();
                    OnFriendsChanged?.Invoke();
                    feedbackMessage = $"✓ 已同意【{req.nickname}】的好友申请，已写入好友列表！";
                    return true;
                }
            }
            feedbackMessage = "未找到对应好友申请。";
            return false;
        }

        public static bool RejectRequest(string playerId, out string feedbackMessage)
        {
            EnsureInitialized();
            for (int i = 0; i < Requests.Count; i++)
            {
                if (Requests[i].playerId == playerId)
                {
                    string name = Requests[i].nickname;
                    Requests.RemoveAt(i);
                    SaveToDisk();
                    OnFriendsChanged?.Invoke();
                    feedbackMessage = $"已拒绝【{name}】的好友申请";
                    return true;
                }
            }
            feedbackMessage = "未找到对应好友申请。";
            return false;
        }

        public static void ResetFriendsData()
        {
            _initialized = true;
            BuildMockDirectory();
            SeedDefaults();
            SaveToDisk();
            OnFriendsChanged?.Invoke();
        }
    }
}
