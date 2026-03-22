-- Seed "Kurry Leaves" restaurant and its full menu
-- Owner: same as "Desi Bites" (926f872c-129d-42c1-8c78-02f4b6f23936)
-- Food category: c931f5f8-dd31-492a-9764-24ab1cbd1344

SET @restaurant_id = UUID();
SET @owner_id = '926f872c-129d-42c1-8c78-02f4b6f23936';
SET @food_cat = 'c931f5f8-dd31-492a-9764-24ab1cbd1344';
SET @now = UTC_TIMESTAMP(6);

-- 1. Create the restaurant
INSERT INTO traditional_eats_restaurant.Restaurants
  (RestaurantId, OwnerId, Name, Description, CuisineType, Address, Latitude, Longitude,
   PhoneNumber, Email, IsActive, Rating, ReviewCount, CreatedAt, UpdatedAt, VendorType)
VALUES
  (@restaurant_id, @owner_id, 'SpiceNRice',
   'Authentic Indian & Pakistani cuisine featuring biryanis, tandoori specialties, curries, and more.',
   'Indian', '6537 W 119th St., Overland Park, KS 66209',
   38.9130, -94.6620,
   '+19132910436', 'info@kurryleaves.com',
   1, 0, 0, @now, @now, 'Food');

SELECT @restaurant_id AS NewRestaurantId;
-- Insert new menu
INSERT INTO traditional_eats_catalog.MenuItems
(MenuItemId, RestaurantId, CategoryId, Name, Description, Price, IsAvailable, DietaryTagsJson, CreatedAt, UpdatedAt)
VALUES

-- === SMALL PLATES ===
(UUID(), @restaurant_id, @food_cat, 'Gobi Manchurian', NULL, 11.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chili Paneer', NULL, 11.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken 65', NULL, 11.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chili Chicken', NULL, 11.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Shrimp 65', NULL, 13.99, 1, '[]', @now, @now),

-- === APPETIZERS ===
(UUID(), @restaurant_id, @food_cat, 'Vegetable Samosa', NULL, 5.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Vegetable Pakora', NULL, 7.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Samosa (3 Pcs)', NULL, 6.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Aloo Tikkiyas', NULL, 6.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Mirchi Pakora', NULL, 6.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Seekh Kabab', NULL, 12.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Hummus Masala', NULL, 8.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Hara Bhara Kabab', NULL, 7.99, 1, '["Vegetarian"]', @now, @now),

-- === CHAAT ===
(UUID(), @restaurant_id, @food_cat, 'Samosa Chaat', NULL, 9.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Tikki Chaat', NULL, 9.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Papdy Chaat', NULL, 9.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Eggplant Chaat', NULL, 9.99, 1, '["Vegetarian"]', @now, @now),

-- === TANDOORI ===
(UUID(), @restaurant_id, @food_cat, 'Tandoori Chicken', NULL, 15.99, 1, '["Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Tikka Kabab', NULL, 15.99, 1, '["Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Achari Chicken Kabab', NULL, 15.99, 1, '["Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Peshawari Chapli Kabab', NULL, 15.99, 1, '["Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lamb Seekh Kabab', NULL, 16.99, 1, '["Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Mixed Grill', NULL, 19.99, 1, '["Gluten-Free"]', @now, @now),

-- === BIRYANI ===
(UUID(), @restaurant_id, @food_cat, 'Chicken Biryani', NULL, 14.99, 1, '["Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Boneless Chicken Biryani', NULL, 15.99, 1, '["Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Vegetable Biryani', NULL, 13.99, 1, '["Vegetarian","Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Egg Biryani', NULL, 14.99, 1, '["Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Shrimp Biryani', NULL, 16.99, 1, '["Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lamb Biryani', NULL, 17.99, 1, '["Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Goat Biryani', NULL, 17.99, 1, '["Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Special Biryani Combo', NULL, 17.99, 1, '["Gluten-Free"]', @now, @now),

-- === CHICKEN CURRIES ===
(UUID(), @restaurant_id, @food_cat, 'Butter Chicken', NULL, 15.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Tikka Masala', NULL, 15.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Vindaloo', NULL, 15.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Korma', NULL, 15.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Saag', NULL, 15.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Chettinad', NULL, 15.99, 1, '[]', @now, @now),

-- === LAMB ===
(UUID(), @restaurant_id, @food_cat, 'Lamb Curry', NULL, 18.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lamb Vindaloo', NULL, 18.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lamb Korma', NULL, 18.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lamb Saag', NULL, 18.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lamb Tikka Masala', NULL, 18.99, 1, '[]', @now, @now),

-- === VEGETARIAN ===
(UUID(), @restaurant_id, @food_cat, 'Chana Masala', NULL, 14.99, 1, '["Vegetarian","Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Dal Makhani', NULL, 14.99, 1, '["Vegetarian","Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Aloo Gobi', NULL, 14.99, 1, '["Vegetarian","Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Saag Paneer', NULL, 15.99, 1, '["Vegetarian","Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Paneer Masala', NULL, 15.99, 1, '["Vegetarian","Gluten-Free"]', @now, @now),

-- === BREAD ===
(UUID(), @restaurant_id, @food_cat, 'Classic Naan', NULL, 2.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Garlic Naan', NULL, 3.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Butter Naan', NULL, 3.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Basmati Rice', NULL, 2.49, 1, '["Vegetarian","Gluten-Free"]', @now, @now),

-- === DESSERT ===
(UUID(), @restaurant_id, @food_cat, 'Gulab Jamun', NULL, 4.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Ras Malai', NULL, 4.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Kheer', NULL, 4.99, 1, '["Vegetarian"]', @now, @now),

-- === DRINKS ===
(UUID(), @restaurant_id, @food_cat, 'Mango Lassi', NULL, 5.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Sweet Lassi', NULL, 4.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chai Tea', NULL, 2.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Soft Drinks', NULL, 2.99, 1, '[]', @now, @now);