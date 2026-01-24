import frida
import sys

def on_message(message, data):
    if message['type'] == 'send':
        print("[*] {0}".format(message['payload']))
    else:
        print(message)

def dump_memory(target_path, output_txt):
    session = frida.spawn([target_path])
    script_code = open('memory_dumper.js').read()
    process = frida.attach(session)
    script = process.create_script(script_code)
    script.on('message', on_message)
    script.load()
    frida.resume(session)
    # Save output to file as needed
