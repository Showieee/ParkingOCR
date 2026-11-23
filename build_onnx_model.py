import os
import copy
import random
import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim
from PIL import Image, ImageDraw, ImageFont, ImageFilter

CLASSES = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"
NUM_CLASSES = len(CLASSES)
CHAR_TO_IDX = {c: i for i, c in enumerate(CLASSES)}

FONT_NAMES = [
    "bahnschrift.ttf", "arialbd.ttf", "arial.ttf", "calibrib.ttf", "calibri.ttf",
    "seguisb.ttf", "segoeuib.ttf", "tahomabd.ttf", "verdanab.ttf", "consola.ttf"
]
FONTS = []
for fname in FONT_NAMES:
    p = os.path.join(r"C:\Windows\Fonts", fname)
    if os.path.exists(p):
        FONTS.append(p)

def render_char(char, font_path=None, size=(32, 32)):
    img = Image.new("L", (120, 120), color=0)
    draw = ImageDraw.Draw(img)
    
    font_size = random.randint(45, 65)
    try:
        font = ImageFont.truetype(font_path, font_size) if font_path else ImageFont.load_default()
    except Exception:
        font = ImageFont.load_default()
        
    bbox = draw.textbbox((0, 0), char, font=font)
    w = bbox[2] - bbox[0]
    h = bbox[3] - bbox[1]
    
    x = (120 - w) // 2 - bbox[0]
    y = (120 - h) // 2 - bbox[1]
    draw.text((x, y), char, fill=255, font=font)
    
    angle = random.uniform(-6, 6)
    img = img.rotate(angle, resample=Image.BILINEAR)
    
    bbox = img.getbbox()
    if bbox:
        cropped = img.crop(bbox)
        cw, ch = cropped.size
        target_dim = 26
        scale = min(target_dim / max(1, cw), target_dim / max(1, ch))
        nw = max(1, int(cw * scale))
        nh = max(1, int(ch * scale))
        resized = cropped.resize((nw, nh), Image.BILINEAR)
        
        canvas = Image.new("L", (32, 32), color=0)
        ox = (32 - nw) // 2
        oy = (32 - nh) // 2
        canvas.paste(resized, (ox, oy))
        res = canvas
    else:
        res = img.resize(size, Image.BILINEAR)
        
    if random.random() < 0.2:
        res = res.filter(ImageFilter.GaussianBlur(radius=random.uniform(0.2, 0.5)))
        
    arr = np.array(res, dtype=np.float32) / 255.0
    return arr

class CharCNN(nn.Module):
    def __init__(self, num_classes=36):
        super().__init__()
        self.features = nn.Sequential(
            nn.Conv2d(1, 32, kernel_size=3, padding=1),
            nn.BatchNorm2d(32),
            nn.ReLU(inplace=True),
            nn.MaxPool2d(2, 2), # 16x16
            
            nn.Conv2d(32, 64, kernel_size=3, padding=1),
            nn.BatchNorm2d(64),
            nn.ReLU(inplace=True),
            nn.MaxPool2d(2, 2), # 8x8
            
            nn.Conv2d(64, 128, kernel_size=3, padding=1),
            nn.BatchNorm2d(128),
            nn.ReLU(inplace=True),
            nn.MaxPool2d(2, 2), # 4x4
        )
        self.classifier = nn.Sequential(
            nn.Dropout(0.15),
            nn.Linear(128 * 4 * 4, 128),
            nn.ReLU(inplace=True),
            nn.Linear(128, num_classes)
        )
        
    def forward(self, x):
        feat = self.features(x)
        feat = torch.flatten(feat, 1)
        return self.classifier(feat)

def main():
    print("Generating dataset...")
    samples_per_class = 250
    X_list = []
    y_list = []
    
    for i, c in enumerate(CLASSES):
        for _ in range(samples_per_class):
            fpath = random.choice(FONTS) if FONTS else None
            arr = render_char(c, fpath)
            X_list.append(arr)
            y_list.append(i)
            
    X = np.array(X_list, dtype=np.float32)[:, np.newaxis, :, :]
    y = np.array(y_list, dtype=np.int64)
    
    indices = np.random.permutation(len(X))
    X = X[indices]
    y = y[indices]
    
    split = int(0.85 * len(X))
    X_train, X_val = torch.tensor(X[:split]), torch.tensor(X[split:])
    y_train, y_val = torch.tensor(y[:split]), torch.tensor(y[split:])
    
    model = CharCNN(num_classes=NUM_CLASSES)
    criterion = nn.CrossEntropyLoss()
    optimizer = optim.AdamW(model.parameters(), lr=0.0015, weight_decay=1e-4)
    
    batch_size = 64
    dataset = torch.utils.data.TensorDataset(X_train, y_train)
    loader = torch.utils.data.DataLoader(dataset, batch_size=batch_size, shuffle=True)
    
    epochs = 8
    best_acc = 0.0
    best_state = None
    
    for epoch in range(epochs):
        model.train()
        total_loss = 0.0
        for bx, by in loader:
            optimizer.zero_grad()
            out = model(bx)
            loss = criterion(out, by)
            loss.backward()
            optimizer.step()
            total_loss += loss.item() * len(bx)
            
        model.eval()
        with torch.no_grad():
            val_out = model(X_val)
            preds = torch.argmax(val_out, dim=1)
            acc = (preds == y_val).float().mean().item()
        print(f"Epoch {epoch+1}/{epochs} - Loss: {total_loss/len(X_train):.4f} - Val Acc: {acc*100:.2f}%")
        if acc >= best_acc:
            best_acc = acc
            best_state = copy.deepcopy(model.state_dict())
            
    out_path = r"ParkingOCR.Core\Resources\char_classifier.onnx"
    model.load_state_dict(best_state)
    model.eval()
    dummy_input = torch.randn(1, 1, 32, 32)
    torch.onnx.export(
        model,
        dummy_input,
        out_path,
        dynamo=False,
        input_names=["input"],
        output_names=["output"],
        dynamic_axes={"input": {0: "batch_size"}, "output": {0: "batch_size"}}
    )
    print(f"Exported model with {best_acc*100:.2f}% validation accuracy to {out_path}")

if __name__ == "__main__":
    main()
