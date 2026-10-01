import urllib.request
import json

url = "https://raw.githubusercontent.com/MassTransit/MassTransit/v8.2.5/src/MassTransit/Middleware/Outbox/BusOutboxNotification.cs"
try:
    response = urllib.request.urlopen(url)
    print(response.read().decode('utf-8'))
except Exception as e:
    print(e)
