import os
from PIL import Image, ImageDraw, ImageFont

def create_license_plate(text, include_euroband=True, is_temporary=False, font_size=56):
    width = 460
    height = 100
    
    img = Image.new("RGB", (width, height), color=(255, 255, 255))
    draw = ImageDraw.Draw(img)
    
    # Outer black border
    draw.rectangle([1, 1, width - 2, height - 2], outline=(0, 0, 0), width=3)
    
    start_x = 20
    
    if include_euroband:
        euro_w = 56
        # EU Blue
        draw.rectangle([3, 3, euro_w, height - 4], fill=(0, 51, 153))
        
        # Yellow stars in circle
        center = (euro_w // 2, 28)
        radius = 14
        import math
        for i in range(12):
            angle = i * math.pi / 6.0
            sx = int(center[0] + radius * math.cos(angle))
            sy = int(center[1] + radius * math.sin(angle))
            draw.ellipse([sx - 1, sy - 1, sx + 1, sy + 1], fill=(255, 204, 0))
            
        # "RO" in white
        try:
            ro_font = ImageFont.truetype(r"C:\Windows\Fonts\arialbd.ttf", 20)
        except:
            ro_font = ImageFont.load_default()
        bbox = draw.textbbox((0, 0), "RO", font=ro_font)
        rw = bbox[2] - bbox[0]
        draw.text(((euro_w - rw) // 2, 62), "RO", fill=(255, 255, 255), font=ro_font)
        
        start_x = euro_w + 24
        
    # License plate font: bahnschrift (DIN 1451) or arial bold
    font_path = r"C:\Windows\Fonts\bahnschrift.ttf"
    if not os.path.exists(font_path):
        font_path = r"C:\Windows\Fonts\arialbd.ttf"
        
    font = ImageFont.truetype(font_path, font_size)
    text_color = (200, 0, 0) if is_temporary else (0, 0, 0)
    
    # Draw plate text
    tokens = text.split()
    curr_x = start_x
    for t in tokens:
        for ch in t:
            bbox = draw.textbbox((0, 0), ch, font=font)
            cw = bbox[2] - bbox[0]
            draw.text((curr_x, 22), ch, fill=text_color, font=font)
            curr_x += cw + 8
        curr_x += 16
        
    return img

def create_car_bumper(plate_img):
    canvas_w, canvas_h = 800, 500
    canvas = Image.new("RGB", (canvas_w, canvas_h), color=(48, 52, 58)) # car bumper dark gray
    draw = ImageDraw.Draw(canvas)
    
    # Bumper styled curves / lines
    draw.line([(0, 150), (canvas_w, 150)], fill=(35, 38, 42), width=4)
    draw.line([(0, 380), (canvas_w, 380)], fill=(35, 38, 42), width=4)
    
    # Recessed plate holder
    pw, ph = plate_img.size
    px = (canvas_w - pw) // 2
    py = 200
    draw.rectangle([px - 8, py - 8, px + pw + 8, py + ph + 8], fill=(20, 20, 20))
    
    canvas.paste(plate_img, (px, py))
    return canvas

def main():
    out_dir = r"ParkingOCR.Tests\TestImages"
    os.makedirs(out_dir, exist_ok=True)
    
    plates = [
        ("bucharest_b123abc.jpg", "B 123 ABC", True, False, False),
        ("county_cj12abc.jpg", "CJ 12 ABC", True, False, False),
        ("county_is99xyz.jpg", "IS 99 XYZ", True, False, False),
        ("no_euroband_tm01aaa.jpg", "TM 01 AAA", False, False, False),
        ("temporary_b123456.jpg", "B 123456", False, True, False),
        ("car_bumper_b777bos.jpg", "B 777 BOS", True, False, True),
    ]
    
    for filename, text, euro, temp, bumper in plates:
        pimg = create_license_plate(text, include_euroband=euro, is_temporary=temp)
        if bumper:
            final_img = create_car_bumper(pimg)
        else:
            final_img = pimg
        out_path = os.path.join(out_dir, filename)
        final_img.save(out_path, quality=95)
        print(f"Generated {out_path}")

if __name__ == "__main__":
    main()
