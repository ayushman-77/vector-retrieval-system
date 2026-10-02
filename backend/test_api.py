import urllib.request
import urllib.error
import json
import time

url = 'http://localhost:5000/api/chat'
data = json.dumps({'query': 'hello'}).encode('utf-8')
headers = {'Content-Type': 'application/json'}
req = urllib.request.Request(url, data=data, headers=headers, method='POST')

print("Sending request...")
start_time = time.time()
try:
    with urllib.request.urlopen(req) as response:
        result = response.read().decode('utf-8')
        print("Success:", result)
except urllib.error.HTTPError as e:
    print(f"HTTP Error {e.code}: {e.read().decode('utf-8')}")
except Exception as e:
    print("Error:", str(e))
print(f"Elapsed time: {time.time() - start_time:.2f} seconds")
