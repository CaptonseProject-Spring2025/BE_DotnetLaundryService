using Google.Cloud.Firestore;
using LaundryService.Domain.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class FirebaseStorageService : IFirebaseStorageService
    {
        private readonly FirestoreDb _firestore;

        public FirebaseStorageService()
        {
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "notification-firebase-adminsdk.json");
            _firestore = FirestoreDb.Create("notification-8a9a5");
        }

        public async Task SaveTokenAsync(string userId, string fcmToken)
        {
            try
            {
                var deviceId = Guid.NewGuid().ToString();

                DocumentReference docRef = _firestore.Collection("user_tokens").Document(userId)
                                                    .Collection("tokens").Document(deviceId);

                await docRef.SetAsync(new
                {
                    Token = fcmToken,
                    CreatedAt = DateTime.UtcNow
                });

                Console.WriteLine("FCMToken saved successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving fcntoken: {ex.Message}");
            }
        }
    }
}
