% FaceDetector.m
% This is based on Hamed Masnadi-Shirazi's code
% for a project he did during his studies at ucsd. 
% http://www-cse.ucsd.edu/classes/fa04/cse252c/projects/hamed.pdf 
% and the Imad Khoury et al who claim this to be
% their own and do not mention Hamed 
% I used the training data provided by Imad.
% http://khoury.imad.googlepages.com/computervision(comp358b)project
% 
% I've optimized the code somewhat, made it more readable
% added comments and suggestions and bit more compliance with the OpenCV 
% and the original paper by Viola & Jones
% 

clear all
global alphaArray;
global thetaArray
global fNbestArray;
global pArray;
global f;
global sc;
load('sc.mat');
sc=selClas;
load('features.mat');
Itest=imread('Obr.jpg');
Iorig = Itest;
%faces=getfaces('faces.pat',24,0);
%Itest=imresize(Itest, [120, 160]);
%Itest = faces(1,:);
%Itest = reshape(Itest,25,25);
imshow(Itest);

Itest=rgb2gray(Itest);
Itest = histeq(Itest); % this is really needed for high contrast images

Itest=double(Itest);

%Itest= normImageF(Itest); % this is a very important step as well
%figure,imshow(Itest,[])
%Itest=cumImageJN(Itest);
        

[row col]=size(Itest);

mindim=min(row,col);
rects=[];
rectcount=0;
% this should be where we compute the integral image
% I don't know why it doesn't work - perhaps because the classifiers
% are trained to work with different thresholds (based upon integral
% images calculated in each subwindow
dropped =0;
for szWin = 50:6:mindim  % enlarge the window by 6px every step
   for xplace=2:10:col-szWin % move the window by 10px x
      for yplace=2:10:row-szWin % move it by 10px y as well

        [Ichunk, rect]=imcrop(Itest,[xplace yplace szWin szWin]);
        %Itest= padJN(Itest);

        cropsize=25;
        Ichunk=imresize(Ichunk,[cropsize,cropsize]);   %resizing the image, should resize the detector!

        Ichunk=cumImageJN(Ichunk);     
        Ichunk=reshape(Ichunk,1,(cropsize)^2);
        result = RunCascade(Ichunk,f,xplace,yplace,rect);
        
        if result >0

            rectcount= rectcount + 1;
            rect;
            rects = [rects rect];
            rectangle('Position', rect,'edgecolor','green');
        else
            dropped = dropped +1;
        end
       
      end
   end
end       % SIZE if
rect
% here we will iterate throught the result set and try to resolve
% overlapping boxes. Since it's a lot easier to do using OOP I'll just keep
% this really basic.
sprintf('dropped windows: %d accepted windows: %d', dropped, rectcount)
break;
%disp('Found the following number of faces: ');
%rectcount
overlappingRects=0;
[cnt cnt] = size(rects);
imshow(Iorig);
for i=1:4:cnt
    overlaps = 0;
    hasChild = 0;
    for j=1:4:cnt
        if i==j continue;
        end
        if(is_equal(rects(i:(i+3)), rects(j:(j+3))))
            overlappingRects = overlappingRects +1;
           overlaps = overlaps+1;%
            sprintf('kwadraty %d i %d pokrywaja sie!', (i-1)/4, (j-1)/4)
            break;
        elseif (is_inner(rects(j:(j+3)), rects(i:(i+3))))
           overlappingRects = overlappingRects +1;
            overlaps = overlaps+1;
            sprintf('%d jest w srodku %d!', (j-1)/4, (i-1)/4)
            break;
        end
    end
    if(overlaps==0)
                rectangle('Position', rects(i:(i+3)), 'edgecolor','red');
end
end
%overlappingRects
%clear size;
%size(rects);