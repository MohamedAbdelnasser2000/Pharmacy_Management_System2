using System.Text.Json;
using IMS.Models;

namespace IMS.Services
{
    public class CartItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string? ImageUrl { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
    }

    public interface IECommerceCartService
    {
        List<CartItem> GetCart(ISession session);
        void AddToCart(ISession session, CartItem item);
        void UpdateQuantity(ISession session, int productId, int quantity);
        void RemoveItem(ISession session, int productId);
        void ClearCart(ISession session);
        int GetCartCount(ISession session);
        decimal GetCartTotal(ISession session);
    }

    public class ECommerceCartService : IECommerceCartService
    {
        private const string CartSessionKey = "ECommerceCart";

        public List<CartItem> GetCart(ISession session)
        {
            var cartJson = session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(cartJson))
                return new List<CartItem>();

            return JsonSerializer.Deserialize<List<CartItem>>(cartJson) ?? new List<CartItem>();
        }

        public void AddToCart(ISession session, CartItem item)
        {
            var cart = GetCart(session);
            var existingItem = cart.FirstOrDefault(c => c.ProductId == item.ProductId);

            if (existingItem != null)
            {
                existingItem.Quantity += item.Quantity;
            }
            else
            {
                cart.Add(item);
            }

            SaveCart(session, cart);
        }

        public void UpdateQuantity(ISession session, int productId, int quantity)
        {
            var cart = GetCart(session);
            var item = cart.FirstOrDefault(c => c.ProductId == productId);

            if (item != null)
            {
                if (quantity <= 0)
                    cart.Remove(item);
                else
                    item.Quantity = quantity;

                SaveCart(session, cart);
            }
        }

        public void RemoveItem(ISession session, int productId)
        {
            var cart = GetCart(session);
            cart.RemoveAll(c => c.ProductId == productId);
            SaveCart(session, cart);
        }

        public void ClearCart(ISession session)
        {
            session.Remove(CartSessionKey);
        }

        public int GetCartCount(ISession session)
        {
            return GetCart(session).Sum(c => c.Quantity);
        }

        public decimal GetCartTotal(ISession session)
        {
            return GetCart(session).Sum(c => c.LineTotal);
        }

        private void SaveCart(ISession session, List<CartItem> cart)
        {
            var cartJson = JsonSerializer.Serialize(cart);
            session.SetString(CartSessionKey, cartJson);
        }
    }
}
