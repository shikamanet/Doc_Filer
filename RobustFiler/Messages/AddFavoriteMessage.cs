using CommunityToolkit.Mvvm.Messaging.Messages;
using RobustFiler.Models;

namespace RobustFiler.Messages;

public class AddFavoriteMessage : ValueChangedMessage<FavoriteItem>
{
    public AddFavoriteMessage(FavoriteItem favoriteItem) : base(favoriteItem)
    {
    }
}
