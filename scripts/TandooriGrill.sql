
SET @restaurant_id = UUID();
SET @owner_id = '926f872c-129d-42c1-8c78-02f4b6f23936';
SET @food_cat = 'c931f5f8-dd31-492a-9764-24ab1cbd1344';
SET @now = UTC_TIMESTAMP(6);


INSERT INTO traditional_eats_restaurant.Restaurants
  (RestaurantId, OwnerId, Name, Description, CuisineType, Address, Latitude, Longitude,
   PhoneNumber, Email, IsActive, Rating, ReviewCount, CreatedAt, UpdatedAt, VendorType)
VALUES
  (@restaurant_id, @owner_id, 'Tandoori Grill',
   'Authentic Indian & Pakistani cuisine featuring tandoori grills, curries, and traditional specialties.',
   'Indian/Pakistani', '12247 W 87th St Pkwy, Lenexa, KS 66215',
   38.9712, -94.7275,
   '+19132327826', 'info@tandoorigrill.com',
   1, 0, 0, @now, @now, 'Food');


-- Optional cleanup
DELETE FROM traditional_eats_catalog.MenuItems
WHERE RestaurantId = @restaurant_id;

INSERT INTO traditional_eats_catalog.MenuItems
(MenuItemId, RestaurantId, CategoryId, Name, Description, Price, IsAvailable, DietaryTagsJson, CreatedAt, UpdatedAt)
VALUES

-- === APPETIZERS ===
(UUID(), @restaurant_id, @food_cat, 'Chicken 65', NULL, 9.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Aloo Samosa (2 pcs)', NULL, 4.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Cocktail Samosa (4 pcs)', NULL, 4.99, 1, '["Vegetarian"]', @now, @now),

-- === GRILL ===
(UUID(), @restaurant_id, @food_cat, 'Chicken Chatkhara', NULL, 17.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Malai Boti', NULL, 17.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Boti', NULL, 17.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Seekh Kabab', NULL, 15.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Beef Seekh Kabab', NULL, 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Beef Chapli Kebab', NULL, 14.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Chapli Kebab', NULL, 14.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Tandoori Grill Shrimps', NULL, 19.99, 1, '[]', @now, @now),

-- === CURRIES ===
(UUID(), @restaurant_id, @food_cat, 'Beef Nihari', NULL, 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Beef Haleem', NULL, 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Paya', NULL, 16.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Butter Chicken (Goat)', NULL, 17.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Tikka Masala', NULL, 17.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Shinwari', NULL, 23.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Mutton Shinwari', NULL, 27.99, 1, '[]', @now, @now),

-- === RICE ===
(UUID(), @restaurant_id, @food_cat, 'Chicken Biryani', NULL, 13.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Plain Basmati Rice', NULL, 4.99, 1, '["Vegetarian","Gluten-Free"]', @now, @now),

-- === VEGETARIAN ===
(UUID(), @restaurant_id, @food_cat, 'Halwa Poori Platter', NULL, 15.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chana Masala', NULL, 10.99, 1, '["Vegetarian","Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Aloo Tarkari', NULL, 10.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Daal Tarka', NULL, 12.99, 1, '["Vegetarian","Gluten-Free"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Mix Vegetable', NULL, 12.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Palak Paneer', NULL, 14.99, 1, '["Vegetarian","Gluten-Free"]', @now, @now),

-- === KIDS ===
(UUID(), @restaurant_id, @food_cat, 'Chicken Nuggets', NULL, 6.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chicken Tenders', NULL, 8.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'French Fries', NULL, 4.99, 1, '["Vegetarian"]', @now, @now),

-- === BREADS ===
(UUID(), @restaurant_id, @food_cat, 'Plain Naan', NULL, 2.49, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Garlic Naan', NULL, 3.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Roghni Naan', NULL, 3.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Puri Paratha', NULL, 3.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Puri', NULL, 1.99, 1, '["Vegetarian"]', @now, @now),

-- === DESSERTS ===
(UUID(), @restaurant_id, @food_cat, 'Kheer', NULL, 6.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Gulab Jamun', NULL, 4.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lab-E-Shereen', NULL, 7.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Gajar Halwa', NULL, 7.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Loki Halwa', NULL, 7.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Rabri', NULL, 7.99, 1, '["Vegetarian"]', @now, @now),

-- === DRINKS ===
(UUID(), @restaurant_id, @food_cat, 'Soda', NULL, 1.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Lemonade', NULL, 1.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Water', NULL, 1.00, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Vimto', NULL, 2.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Jarrito Mexican Soda', NULL, 2.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Mint Margarita', NULL, 5.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Mango Milkshake', NULL, 6.99, 1, '[]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Mango Lassi', NULL, 6.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Sweet Lassi', NULL, 5.99, 1, '["Vegetarian"]', @now, @now),
(UUID(), @restaurant_id, @food_cat, 'Chai/Tea', NULL, 2.50, 1, '["Vegetarian"]', @now, @now);