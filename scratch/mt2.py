import urllib.request

url = "https://raw.githubusercontent.com/MassTransit/MassTransit/v8.3.0/src/MassTransit/Middleware/Outbox/BusOutboxNotification.cs"
try:
    response = urllib.request.urlopen(url)
    lines = response.read().decode('utf-8').split('\n')
    for i, line in enumerate(lines):
        print(f"{i+1}: {line}")
except Exception as e:
    print(e)
