import json
import hashlib
import os

def file_hashes(file_path):
    hashes = {}
    with open(file_path, 'rb') as f:
        data = f.read()
        hashes['md5'] = hashlib.md5(data).hexdigest()
        hashes['sha1'] = hashlib.sha1(data).hexdigest()
        hashes['sha256'] = hashlib.sha256(data).hexdigest()
    return hashes

def success_score(output_dir):
    for root, _, files in os.walk(output_dir):
        for f in files:
            if os.path.getsize(os.path.join(root, f)) > 0:
                return 1
    return 0

def generate_audit_report(file_path, packer, engine, output_dir, report_path):
    report = {
        'file_hashes': file_hashes(file_path),
        'packer_detected': packer,
        'engine_used': engine,
        'success_score': success_score(output_dir)
    }
    with open(report_path, 'w') as f:
        json.dump(report, f, indent=2)
