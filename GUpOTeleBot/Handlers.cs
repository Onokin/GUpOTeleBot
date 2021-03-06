using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace GUpOTeleBot
{
    class Handlers
    {
        public static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var handler = update.Type switch
            {
                // UpdateType.Unknown:
                UpdateType.ChannelPost => BotOnMessageReceived(botClient, update.Message),
                // UpdateType.EditedChannelPost:
                // UpdateType.ShippingQuery:
                // UpdateType.PreCheckoutQuery:
                // UpdateType.Poll:
                UpdateType.Message => BotOnMessageReceived(botClient, update.Message),
                UpdateType.EditedMessage => BotOnMessageReceived(botClient, update.EditedMessage),
                UpdateType.CallbackQuery => BotOnCallbackQueryReceived(botClient, update.CallbackQuery),
                UpdateType.InlineQuery => BotOnInlineQueryReceived(botClient, update.InlineQuery),
                UpdateType.ChosenInlineResult => BotOnChosenInlineResultReceived(botClient, update.ChosenInlineResult),
                _ => UnknownUpdateHandlerAsync(botClient, update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(botClient, exception, cancellationToken);
            }
        }

        private static async Task BotOnMessageReceived(ITelegramBotClient botClient, Message message)
        {
            Console.WriteLine($"Receive message type: {message.Type}");
            if (message.Type != MessageType.Text)
                return;

            const string keyWords = "факультатив|шестой школьный день";


            if (message.Text.TrimStart()[0] == '/')
            {
                var action = (message.Text.Split(' ').First()) switch
                {
                    "/start" => TextResponse(botClient, message, "Здравствуйте!\n\nНапишите ваш вопрос и мы ответим Вам в ближайшее время.\n Для отображения дерева введите /search"),
                    "/inline" => SendInlineKeyboard(botClient, message),
                    "/keyboard" => SendReplyKeyboard(botClient, message),
                    "/remove" => RemoveKeyboard(botClient, message),
                    "/photo" => SendFile(botClient, message),
                    "/request" => RequestContactAndLocation(botClient, message),
                    "/search" => SendInlineKeyInfoTree(botClient, message),
                    _ => Usage(botClient, message)
                };
                var sentMessage = await action;
                Console.WriteLine($"The message was sent with id: {sentMessage.MessageId}");
            }
            else
            {
                var keys = keyWords.Split("|");
                foreach (var item in keys)
                {
                    for (int i = 0; i <= message.Text.Length; i++)
                    {
                        //Console.WriteLine($"{message.Text.Substring(i,item.Length)}: {item} {FuzzySearch.LevenshteinDistance(message.Text.Substring(i,item.Length), item)}");
                        var compareStr = message.Text;
                        var aux = 0;
                        if (item.Length < compareStr.Length)
                        {
                            compareStr = message.Text.Substring(i);
                            if (compareStr.Length < item.Length)
                                compareStr = compareStr.PadRight(item.Length - compareStr.Length, ' ');
                            else
                                compareStr = compareStr.Substring(0, item.Length);
                        }
                        aux = FuzzySearch.LevenshteinDistance(compareStr, item);
                        if (aux < 6)
                        {
                            Console.WriteLine($"found in: {compareStr} | {item} | {aux}");
                            var action = (item) switch
                            {
                                "факультатив" => TextResponse(botClient, message, "Инфа о факультативах"),
                                "шестой школьный день" => TextResponse(botClient, message, "Инфа о шестом школьном дне"),
                                _ => Usage(botClient, message)
                            };
                            var sentMessage = await action;
                            Console.WriteLine($"The message was sent with id: {sentMessage.MessageId}");
                            break;
                        }
                    }


                    //int ks = 0;
                    //for (int i = 0; i < message.Text.Length; i++)
                    //{
                    //    if (message.Text.ToLower()[i] == item[0])
                    //    {
                    //        int aux = 0;
                    //        foreach (var c in item)
                    //        {
                    //            if (message.Text.Length > i + aux)
                    //                if (message.Text.ToLower()[i + aux++] == c)
                    //                    ks++;

                    //        }
                    //    }
                    //}
                    //if (item.Length / ks > 0.8)
                    //{
                    //    var action = (item) switch
                    //    {
                    //        "факультатив" => TextResponse(botClient, message, "Инфа о факультативах"),
                    //        "шестой школьный день" => TextResponse(botClient, message, "Инфа о шестом школьном дне"),
                    //        _ => Usage(botClient, message)
                    //    };
                    //    var sentMessage = await action;
                    //    Console.WriteLine($"The message was sent with id: {sentMessage.MessageId}");
                    //}
                }

                //var keys = keyWords.Split(", ");
                //foreach (var item in keys)
                //{
                //    if (message.Text.ToLower().Contains(item))
                //    {
                //        Console.WriteLine(item);
                //    }
                //}
            }


            static async Task<Message> SendInlineKeyInfoTree(ITelegramBotClient botClient, Message message)
            {
                await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    // first row
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Аттестация", "12345"),
                        InlineKeyboardButton.WithUrl("Сайт МОИРО", "https://moiro.by/"),
                    },
                });

                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: "Выберите к какой теме относится Ваш вопрос?",
                                                            replyMarkup: inlineKeyboard);
            }


            static async Task<Message> TextResponse(ITelegramBotClient botClient, Message message, string lol)
            {
                await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: lol);
            }



            // Send inline keyboard
            // You can process responses in BotOnCallbackQueryReceived handler
            static async Task<Message> SendInlineKeyboard(ITelegramBotClient botClient, Message message)
            {
                await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                // Simulate longer running task
                await Task.Delay(500);

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    // first row
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("1.1", "11"),
                        InlineKeyboardButton.WithCallbackData("1.2", "12"),
                    },
                    // second row
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("2.1", "21"),
                        InlineKeyboardButton.WithCallbackData("2.2", "22"),
                    },
                });

                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: "Choose",
                                                            replyMarkup: inlineKeyboard);
            }

            static async Task<Message> SendReplyKeyboard(ITelegramBotClient botClient, Message message)
            {
                var replyKeyboardMarkup = new ReplyKeyboardMarkup(
                    new KeyboardButton[][]
                    {
                        new KeyboardButton[] { "1.1", "1.2" },
                        new KeyboardButton[] { "2.1", "2.2" },
                    })
                {
                    ResizeKeyboard = true
                };

                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: "Choose",
                                                            replyMarkup: replyKeyboardMarkup);
            }

            static async Task<Message> RemoveKeyboard(ITelegramBotClient botClient, Message message)
            {
                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: "Removing keyboard",
                                                            replyMarkup: new ReplyKeyboardRemove());
            }

            static async Task<Message> SendFile(ITelegramBotClient botClient, Message message)
            {
                await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.UploadPhoto);

                const string filePath = @"Files/tux.png";
                using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var fileName = filePath.Split(Path.DirectorySeparatorChar).Last();

                return await botClient.SendPhotoAsync(chatId: message.Chat.Id,
                                                      photo: new InputOnlineFile(fileStream, fileName),
                                                      caption: "Nice Picture");
            }

            static async Task<Message> RequestContactAndLocation(ITelegramBotClient botClient, Message message)
            {
                var RequestReplyKeyboard = new ReplyKeyboardMarkup(new[]
                {
                    KeyboardButton.WithRequestLocation("Location"),
                    KeyboardButton.WithRequestContact("Contact"),
                });

                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: "Who or Where are you?",
                                                            replyMarkup: RequestReplyKeyboard);
            }

            static async Task<Message> Usage(ITelegramBotClient botClient, Message message)
            {
                //const string usage = "Usage:\n" +
                //                     "/inline   - send inline keyboard\n" +
                //                     "/keyboard - send custom keyboard\n" +
                //                     "/remove   - remove custom keyboard\n" +
                //                     "/photo    - send a photo\n" +
                //                     "/request  - request location or contact";
                const string usage = "Нет ответа.";
                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: usage,
                                                            replyMarkup: new ReplyKeyboardRemove());
            }
        }

        // Process Inline Keyboard callback data
        private static async Task BotOnCallbackQueryReceived(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {

            Console.WriteLine(callbackQuery.Data);
            var action = (callbackQuery.Data) switch
            {
                "12345" => SendInlineKeyInfoTree(botClient, callbackQuery.Message),
                "/search" => SendInlineKeyInfoTree(botClient, callbackQuery.Message),
                _ => botClient.SendTextMessageAsync(chatId: callbackQuery.Message.Chat.Id,
                                                            text: "Nothing")
            };
            var sentMessage = await action;
            Console.WriteLine($"The message was sent with id: {sentMessage.MessageId}");


            static async Task<Message> SendInlineKeyInfoTree(ITelegramBotClient botClient, Message message)
            {
                await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    // first row
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Нормативное\n\n и методичческое\n\n обеспечение", "1"),
                    },
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Контакты методиста МОИРО для консультаций", "2"),
                    },
                    new []
                    {                        
                        InlineKeyboardButton.WithCallbackData("Примерные задания на экзамене", "3"),
                    },
                });

                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: "Выберите к какой теме относится Ваш вопрос?",
                                                            replyMarkup: inlineKeyboard);
            }




            //await botClient.AnswerCallbackQueryAsync(
            //    callbackQueryId: callbackQuery.Id,
            //    text: $"Received {callbackQuery.Data}");

            //await botClient.SendTextMessageAsync(
            //    chatId: callbackQuery.Message.Chat.Id,
            //    text: $"Received {callbackQuery.Data}");
        }

        private static async Task BotOnInlineQueryReceived(ITelegramBotClient botClient, InlineQuery inlineQuery)
        {
            Console.WriteLine($"Received inline query from: {inlineQuery.From.Id}");

            InlineQueryResultBase[] results = {
                // displayed result
                new InlineQueryResultArticle(
                    id: "3",
                    title: "TgBots",
                    inputMessageContent: new InputTextMessageContent(
                        "hello"
                    )
                )
            };

            await botClient.AnswerInlineQueryAsync(
                inlineQueryId: inlineQuery.Id,
                results: results,
                isPersonal: true,
                cacheTime: 0);
        }

        private static Task BotOnChosenInlineResultReceived(ITelegramBotClient botClient, ChosenInlineResult chosenInlineResult)
        {
            Console.WriteLine($"Received inline result: {chosenInlineResult.ResultId}");
            return Task.CompletedTask;
        }

        private static Task UnknownUpdateHandlerAsync(ITelegramBotClient botClient, Update update)
        {
            Console.WriteLine($"Unknown update type: {update.Type}");
            return Task.CompletedTask;
        }
    }
}
