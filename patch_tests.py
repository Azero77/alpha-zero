import re

with open('tests/Modules/Documents/Documents.UnitTests/DocumentTests.cs', 'r') as f:
    content = f.read()

# Replace `null, _clock` with `null, false, _clock`
content = content.replace('null, _clock);', 'null, false, _clock);')

with open('tests/Modules/Documents/Documents.UnitTests/DocumentTests.cs', 'w') as f:
    f.write(content)
