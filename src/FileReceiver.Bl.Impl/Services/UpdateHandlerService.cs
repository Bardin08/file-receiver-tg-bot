using System;
using System.Threading.Tasks;

using AutoMapper;

using FileReceiver.Bl.Abstract.Factories;
using FileReceiver.Bl.Abstract.Handlers;
using FileReceiver.Bl.Abstract.Services;
using FileReceiver.Common.Extensions;
using FileReceiver.Common.Models;
using FileReceiver.Dal.Abstract.Repositories;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace FileReceiver.Bl.Impl.Services
{
    public class UpdateHandlerService : IUpdateHandlerService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ICommandHandlerFactory _commandHandlerFactory;
        private readonly IUpdateHandlerFactory _updateHandlerFactory;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IMapper _mapper;

        public UpdateHandlerService(
            ITelegramBotClient botClient,
            ICommandHandlerFactory commandHandlerFactory,
            IUpdateHandlerFactory updateHandlerFactory,
            ITransactionRepository transactionRepository,
            IMapper mapper)
        {
            _botClient = botClient;
            _commandHandlerFactory = commandHandlerFactory;
            _updateHandlerFactory = updateHandlerFactory;
            _transactionRepository = transactionRepository;
            _mapper = mapper;
        }

        public async Task HandleUpdateAsync(Update update)
        {
            if (update?.Message == null)
            {
                return;
            }

            // TODO: Add more secure way to receive user's Id
            var userId = update.Message.From.Id;

            if ((await TryGetUpdateHandlerAsync(userId)) is { } updateHandler)
            {
                await updateHandler.HandleUpdateAsync(update);
                return;
            }

            if (await TryGetCommandHandlerAsync(update.Message) is { } commandHandler)
            {
                // TODO: Create a command data set(maybe done with one more factory)
                await commandHandler.HandleCommandAsync(update);
                return;
            }

            await SuitableHandlerNotFound(update.Message.From.Id);
        }

        private async Task SuitableHandlerNotFound(long fromId)
        {
            // TODO: Replace plain text literals with a resources calling
            // Maybe send not a plain text but a GIF or smth like that?
            await _botClient.SendTextMessageAsync(fromId, "Sorry, I can`t understand you!");
        }

        private async Task<IUpdateHandler> TryGetUpdateHandlerAsync(long userId)
        {
            var lastActiveTransactionForUser = await _transactionRepository.GetLastActiveTransactionByUserId(userId);
            if (lastActiveTransactionForUser == null)
            {
                return null;
            }

            return _updateHandlerFactory.CreateUpdateHandler(
                _mapper.Map<TransactionModel>(lastActiveTransactionForUser));
        }

        private Task<ICommandHandler> TryGetCommandHandlerAsync(Message message)
        {
            // TODO: Add a command detection logic
            return Task.FromResult(_commandHandlerFactory.CreateCommandHandler(message.Text.GetCommandFromMessage()));
        }
    }
}