﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GammaLibrary.Extensions;
using WFBot.Connector;
using WFBot.MahuaEvents;
using WFBot.Utils;
using Timer = System.Timers.Timer;

namespace WFBot.Features.Utils
{
    public static class MessengerHandlers
    {
        public static Action<string> DebugAlternateHandler;
        public static Action<string> MessageAlternateHandler;
    }

    public static class Messenger
    {
        public static Dictionary<string, int> GroupCallDic = new Dictionary<string, int>();
        public static Dictionary<string, string> PrivateMessageDictionary = new Dictionary<string, string>();

        static Messenger()
        {
            // 大家都知道你很蠢啦

        }

        public static void IncreaseCallCounts(string group)
        {
            if (GroupCallDic.ContainsKey(group))
            {
                GroupCallDic[group]++;
            }
            else
            {
                GroupCallDic[group] = 1;
            }
            Task.Delay(TimeSpan.FromSeconds(60)).ContinueWith(task => GroupCallDic[group]--);

        }

        public static void SendDebugInfo(string content)
        {
            if (Config.Instance.QQ.IsNumber())
                SendPrivate(Config.Instance.QQ, content);
            Trace.WriteLine($"{content}", "Message");
        }

        public static void SendPrivate(UserID humanQQ, string content)
        {
            ConnectorManager.Connector.SendPrivateMessage(humanQQ, content);
            // todo
        }

        private static readonly Dictionary<GroupID, string> previousMessageDic = new Dictionary<GroupID, string>();
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void SendGroup(GroupID g, string content)
        {
            if (MessengerHandlers.MessageAlternateHandler != null)
            {
                MessengerHandlers.MessageAlternateHandler(content);
                return;
            }

            var qq = g.ID;
            if (previousMessageDic.ContainsKey(qq) && content == previousMessageDic[qq]) return;

            previousMessageDic[qq] = content;

            ConnectorManager.Connector.SendGroupMessage(g, content);

            IncreaseCallCounts(g);

            //Thread.Sleep(1000); //我真的很生气 为什么傻逼tencent服务器就不能让我好好地发通知 NMSL
        }

        public static void Broadcast(string content)
        {
            Task.Factory.StartNew(() =>
            {
                var count = 0;
                foreach (var group in Config.Instance.WFGroupList)
                {
                    if (count > 20 && content.StartsWith("机器人开始了自动更新")) return;

                    var sb = new StringBuilder();
                    sb.AppendLine(content);
                    if (count > 10) sb.AppendLine($"发送次序: {count}(与真实延迟了{7 * count}秒)");
                    sb.AppendLine($"如果想要获取更好的体验,请自行部署.");
                    SendGroup(group, sb.ToString().Trim());
                    count++;
                    Thread.Sleep(7000); //我真的很生气 为什么傻逼tencent服务器就不能让我好好地发通知 NMSL
                }
            }, TaskCreationOptions.LongRunning);
        }

        public static void SendBotStatus(GroupID group)
        {
            var sb = new StringBuilder();
            var q1 = Task.Run(() => WebHelper.TryGet("https://warframestat.us"));
            var q2 = Task.Run(() => WebHelper.TryGet("https://api.warframe.market/v1/items/valkyr_prime_set/orders?include=item"));
            // var q3 = Task.Run(() => WebHelper.TryGet("https://api.richasy.cn/wfa/rm/riven"));
            // var q4 = Task.Run(() => WebHelper.TryGet("https://10o.io/kuvalog.json"));
            Task.WaitAll(q1, q2/*, q3, q4*/);

            var apistat = q1.Result;
            var wmstat = q2.Result;
            // var wfastat = q3.Result;
            // var kuvastat = q4.Result;
            if (apistat.IsOnline && wmstat.IsOnline /*&& wfastat.IsOnline && kuvastat.IsOnline*/)
            {
                sb.AppendLine("机器人状态: 一切正常");
            }
            else
            {
                sb.AppendLine("机器人状态: 不正常");
            }

            sb.AppendLine($"插件版本: *不知道*");

            sb.AppendLine($"    任务API: {apistat.Latency}ms [{(apistat.IsOnline ? "在线" : "离线")}]");
            sb.AppendLine($"    WarframeMarket: {wmstat.Latency}ms [{(wmstat.IsOnline ? "在线" : "离线")}]");
            // sb.AppendLine($"    WFA紫卡市场: {wfastat.Latency}ms [{(wfastat.IsOnline ? "在线" : "离线")}]");
            // sb.AppendLine($"    赤毒/仲裁API: {kuvastat.Latency}ms [{(kuvastat.IsOnline ? "在线" : "离线")}]");
            var commit = CommitsGetter.Get("https://api.github.com/repos/TRKS-Team/WFBot/commits")?.Format() ?? "GitHub Commit 获取异常, 可能是请求次数过多, 如果你是机器人主人, 解决方案请查看 FAQ.";
            sb.AppendLine(commit);
            sb.ToString().Trim().AddPlatformInfo().SendToGroup(group);
        }

        public static void SendHelpdoc(GroupID group)
        {
            SendGroup(@group, @"欢迎查看机器人唯一指定帮助文档
宣传贴地址: https://warframe.love/thread-230.htm
在线最新文档: https://github.com/TRKS-Team/WFBot/blob/universal/README.md
项目地址: https://github.com/TRKS-Team/WFBot
赞助(乞讨)地址: https://afdian.net/@TheRealKamisama
您的赞助会成为我们维护本项目的动力.
本机器人为公益项目, 间断维护中.
如果你想给你的群也整个机器人, 请在上方项目地址了解");
            if (File.Exists("data/image/帮助文档.png"))
            {
                SendGroup(@group, @"[CQ:image,file=\帮助文档.png]");
            }
            else
            {
                SendGroup(@group, @"作者: TheRealKamisama
参数说明: <>为必填参数, []为选填参数, {}为附加选填参数, ()为补充说明
如果群里没有自动通知 请务必检查是否启用了通知功能
    /遗物 <关键词> | 查询遗物的内容
    /wiki [关键词] | 搜索 wiki 上的页面
    /午夜电波 | 每日每周即将过期的挑战
    /机器人状态 | 机器人目前的运行状态
    /警报 | 所有警报
    /入侵 | 所有入侵
    /突击 | 所有突击
    /活动 | 所有活动
    /虚空商人 | 奸商的状态
    /平原 | 地球&金星&火卫二平原的时间循环
    /查询 <物品名称> {-qr} {-b} | 查询 WarframeMarket, 查询未开紫卡请输入: 手枪未开紫卡
    /紫卡 <武器名称> | 紫卡市场 数据来自 WFA 紫卡市场
    /地球赏金 [1-5]| 地球平原的全部/单一赏金任务
    /金星赏金 [1-5]| 金星平原的全部/单一赏金任务
    /裂隙 | 查询全部裂隙
    /翻译 <关键词> |（eg. 致残突击 犀牛prime) 中 -> 英 / 英 -> 中 翻译
    /小小黑 小小黑的信息
*私聊*管理命令:
    /添加群 ******* 群号 | 启用 [群号] 对应的群的通知功能
    /删除群 ******* 群号 | 禁用 [群号] 对应的群的通知功能
    没有启用通知的群不会收到机器人的任务提醒
");
            }
        }

        /* 当麻理解不了下面的代码 */
        // 现在可以了
        public static void SendToGroup(this string content, GroupID qq)
        {
            SendGroup(qq, content);
        }

        public static void SendToPrivate(this string content, UserID qq)
        {
            SendPrivate(qq, content);
        }


        /*
        public static void SuperBroadcast(string content)
        {
            var groups = GetGroups().Select(g => g.Group);
            Task.Factory.StartNew(() =>
            {
                foreach (var group in groups)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(content);
                    SendGroup(group.ToGroupNumber(), sb.ToString().Trim());
                    Thread.Sleep(7000); //我真的很生气 为什么傻逼tencent服务器就不能让我好好地发通知 NMSL
                }
            }, TaskCreationOptions.LongRunning);
        }
        */
    }

    public struct GroupID
    {
        public uint ID { get; }

        public GroupID(uint id)
        {
            ID = id;
        }

        public static implicit operator long(GroupID id)
        {
            return id.ID;
        }

        public static implicit operator uint(GroupID id)
        {
            return id.ID;
        }

        public static implicit operator string(GroupID id)
        {
            return id.ToString();
        }

        public static implicit operator GroupID(long id)
        {
            return new GroupID((uint) id);
        }

        public static implicit operator GroupID(uint id)
        {
            return new GroupID(id);
        }

        public static implicit operator GroupID(string id)
        {
            return new GroupID(id.ToUInt());
        }

        public override string ToString()
        {
            return ID.ToString();
        }
    }

    public struct UserID
    {
        public uint ID { get; }

        public UserID(uint id)
        {
            ID = id;
        }

        public static implicit operator long(UserID id)
        {
            return id.ID;
        }

        public static implicit operator uint(UserID id)
        {
            return id.ID;
        }

        public static implicit operator string(UserID id)
        {
            return id.ToString();
        }

        public static implicit operator UserID(long id)
        {
            return new UserID((uint)id);
        }

        public static implicit operator UserID(uint id)
        {
            return new UserID(id);
        }

        public static implicit operator UserID(string id)
        {
            return new UserID(id.ToUInt());
        }

        public override string ToString()
        {
            return ID.ToString();
        }
    }
}
